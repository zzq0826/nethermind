using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Core.Attributes;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Int256;
using Nethermind.Logging;
using Org.BouncyCastle.Crypto.Paddings;

namespace Nethermind.Evm
{
    enum SectionDividor : byte
    {
        Terminator  = 0,
        CodeSection = 1,
        DataSection = 2,
        TypeSection = 3,
    }
    public class EofHeader
    {
        #region public construction properties
        public int TypeSize { get; set; }
        public int[] CodeSize { get; set; }
        public int CodesSize => CodeSize.Sum();
        public int DataSize { get; set; }
        public byte Version { get; set; }
        public int HeaderSize => DataSize == 0
                        ? 7     // MagicLength + Version + 1 * (SectionSeparator + SectionSize) + HeaderTerminator = 2 + 1 + 1 * (1 + 2) + 1 = 7
                        : 10;
        public int ContainerSize => TypeSize + CodesSize + DataSize;
        #endregion

        #region Equality methods
        public override bool Equals(object? obj)
            => this.GetHashCode() == obj.GetHashCode();
        public override int GetHashCode()
            => CodeSize.GetHashCode() ^ DataSize.GetHashCode();
        #endregion

        #region Sections Offsets
        public (int Start, int Size) TypeSectionOffsets => (HeaderSize, TypeSize);
        public (int Start, int Size) CodeSectionOffsets => (HeaderSize + TypeSize, CodesSize);
        public (int Start, int Size) DataSectionOffsets => (HeaderSize + TypeSize + CodesSize, ContainerSize);
        public (int Start, int Size) this[int i] => (HeaderSize + TypeSize + CodeSize.Take(i).Sum(), CodeSize[i]);
        #endregion
    }

    public class EvmObjectFormat
    {
        private readonly ILogger _logger;
        private bool LoggingEnabled => _logger is not null;
        public EvmObjectFormat(ILogger logger = null)
            => _logger = logger;

        // magic prefix : EofFormatByte is the first byte, EofFormatDiff is chosen to diff from previously rejected contract according to EIP3541
        private const byte EofMagicLength = 2;
        private const byte EofFormatByte = 0xEF;
        private const byte EofFormatDiff = 0x00;
        private byte[] EofMagic => new byte[] { EofFormatByte, EofFormatDiff };

        public bool HasEOFFormat(ReadOnlySpan<byte> code) => code.Length > EofMagicLength && code.StartsWith(EofMagic);
        public bool ExtractHeader(ReadOnlySpan<byte> code, out EofHeader header)
        {
            if (!HasEOFFormat(code))
            {
                if (LoggingEnabled)
                {
                    _logger.Trace($"EIP-3540 : Code doesn't start with Magic byte sequence expected {EofMagic.ToHexString(true)} ");
                }
                header = null; return false;
            }

            int codeLen = code.Length;

            int i = EofMagicLength;
            byte EOFVersion = code[i++];

            header = new EofHeader
            {
                Version = EOFVersion
            };

            switch (EOFVersion)
            {
                case 1:
                    return HandleEOF1(code, ref header, codeLen, ref i);
                default:
                    if (LoggingEnabled)
                    {
                        _logger.Trace($"EIP-3540 : Code has wrong EOFn version expected {1} but found {EOFVersion}");
                    }
                    header = null; return false;
            }
        }

        private bool HandleEOF1(ReadOnlySpan<byte> code, ref EofHeader header, int codeLen, ref int i)
        {
            bool continueParsing = true;

            List<int> CodeSections = new();
            int? TypeSections = null;
            int? DataSections = null;

            while (i < codeLen && continueParsing)
            {
                var sectionKind = (SectionDividor)code[i];
                i++;

                switch (sectionKind)
                {
                    case SectionDividor.Terminator:
                        {
                            if (CodeSections.Count == 0 || CodeSections[1] == 0)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : CodeSection size must follow a CodeSection, CodeSection length was {header.CodeSize}");
                                }
                                header = null; return false;
                            }

                            if (CodeSections.Count > 1 && CodeSections.Count != (TypeSections / 2))
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3750: Code Sections count must ");
                                }
                                header = null; return false;
                            }

                            if (CodeSections.Count > 1024)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-4750 : Code section count limit exceeded only 1024 allowed but found {CodeSections.Count}");
                                }
                                header = null; return false;
                            }
                            header.CodeSize = CodeSections.ToArray();
                            header.TypeSize = TypeSections.Value;
                            header.DataSize = DataSections ?? 0;
                            continueParsing = false;
                            break;
                        }
                    case SectionDividor.TypeSection:
                        {
                            if (TypeSections is not null)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : container must have at max 1 DataSection but found more");
                                }
                                header = null; return false;
                            }

                            if (i + 2 > codeLen)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3750: type section code incomplete, failed parsing type section");
                                }
                                header = null; return false;
                            }

                            var typeSectionSlice = code.Slice(i, 2);
                            var currenttypeSection = (ushort)((typeSectionSlice[0] << 8) | typeSectionSlice[1]);
                            TypeSections = currenttypeSection;
                            break;
                        }
                    case SectionDividor.CodeSection:
                        {
                            if (TypeSections is null )
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3750 : CodeSection size must follow a TypeSection, TypeSection length was {0}");
                                }
                                header = null; return false;
                            }

                            if (i + 2 > codeLen)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : container code incomplete, failed parsing code section");
                                }
                                header = null; return false;
                            }

                            var codeSectionSlice = code.Slice(i, 2);
                            var currentCodeSize = (ushort)((codeSectionSlice[0] << 8) | codeSectionSlice[1]);
                            CodeSections.Add(currentCodeSize);

                            if (currentCodeSize == 0) // code section must be non-empty (i.e : size > 0)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : CodeSection size must be strictly bigger than 0 but found 0");
                                }
                                header = null; return false;
                            }

                            i += 2;
                            break;
                        }
                    case SectionDividor.DataSection:
                        {
                            // data-section must come after code-section and there can be only one data-section
                            if (CodeSections.Count == 0 || TypeSections is null)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : DataSection size must follow a CodeSection, CodeSection length was {header.CodeSize}");
                                }
                                header = null; return false;
                            }
                            if (DataSections is not null)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : container must have at max 1 DataSection but found more");
                                }
                                header = null; return false;
                            }

                            if (i + 2 > codeLen)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : container code incomplete, failed parsing data section");
                                }
                                header = null; return false;
                            }

                            var dataSectionSlice = code.Slice(i, 2);
                            var currentDataSize = (ushort)((dataSectionSlice[0] << 8) | dataSectionSlice[1]);
                            DataSections = currentDataSize;

                            if (currentDataSize == 0) // if declared data section must be non-empty
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-3540 : DataSection size must be strictly bigger than 0 but found 0");
                                }
                                header = null; return false;
                            }

                            i += 2;
                            break;
                        }
                    default: // if section kind is anything beside a section-limiter or a terminator byte we return false
                        {
                            if (LoggingEnabled)
                            {
                                _logger.Trace($"EIP-3540 : Encountered incorrect Section-Kind {sectionKind}, correct values are [{SectionDividor.CodeSection}, {SectionDividor.DataSection}, {SectionDividor.Terminator}]");
                            }

                            header = null; return false;
                        }
                }
            }
            var contractBody = code[i..];

            var calculatedCodeLen = header.TypeSize + header.CodeSize.Sum() + (int)header.DataSize;
            if(header.TypeSize != 0 && (contractBody[0] != 0 && contractBody[1] != 0)) {
                if (LoggingEnabled)
                {
                    _logger.Trace($"EIP-4750: Invalid Type Section expected {(0, 0)} but found {(contractBody[0], contractBody[1])}");
                }
            }

            if (contractBody.Length == 0 || calculatedCodeLen != contractBody.Length)
            {
                if (LoggingEnabled)
                {
                    _logger.Trace($"EIP-3540 : SectionSizes indicated in bundeled header are incorrect, or ContainerCode is incomplete");
                }
                header = null; return false;
            }
            return true;
        }

        public bool ValidateEofCode(ReadOnlySpan<byte> code) => ExtractHeader(code, out _);
        public bool ValidateInstructions(ReadOnlySpan<byte> code, out EofHeader header, IReleaseSpec spec)
        {
            // check if code is EOF compliant
            if(!spec.IsEip3540Enabled)
            {
                header = null;
                return false;
            }

            if (ExtractHeader(code, out header))
            {
                if(!spec.IsEip3670Enabled)
                {
                    return true;
                }

                var (startOffset, endOffset) = header.CodeSectionOffsets;
                Instruction? opcode = null;
                HashSet<Range> immediates = new HashSet<Range>();
                HashSet<Int32> rjumpdests = new HashSet<Int32>();
                for (int i = startOffset; i < endOffset;)
                {
                    opcode = (Instruction)code[i];

                    // validate opcode
                    if (!Enum.IsDefined(opcode.Value))
                    {
                        if (LoggingEnabled)
                        {
                            _logger.Trace($"EIP-3670 : CodeSection contains undefined opcode {opcode}");
                        }
                        return false;
                    }

                    if (spec.IsEip4200Enabled)
                    {
                        if (opcode is Instruction.RJUMP or Instruction.RJUMPI)
                        {
                            if (i + 3 > endOffset)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-4200 : Static Relative Jump Argument underflow");
                                }
                                return false;
                            }

                            var offset = code[(i + 1)..(i + 3)].ReadEthInt16();
                            immediates.Add(new Range(i + 1, i + 2));
                            var rjumpdest = offset + 3 + i;
                            rjumpdests.Add(rjumpdest);
                            if (rjumpdest < startOffset || rjumpdest >= endOffset)
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-4200 : Static Relative Jump Destination outside of Code bounds");
                                }
                                return false;
                            }
                            i += 2;
                        }
                    }

                    if (opcode is >= Instruction.PUSH1 and <= Instruction.PUSH32)
                    {
                        int len = code[i] - (int)Instruction.PUSH1 + 1;
                        immediates.Add(new Range(i + 1, i + len));
                        i += len;
                        if (i >= endOffset)
                        {
                            if (LoggingEnabled)
                            {
                                _logger.Trace($"EIP-3670 : Last opcode {opcode} in CodeSection should be either [{Instruction.STOP}, {Instruction.RETURN}, {Instruction.REVERT}, {Instruction.INVALID}, {Instruction.SELFDESTRUCT}");
                            }
                            return false;
                        }
                    }
                    i++;
                }

                bool endCorrectly = opcode switch
                {
                    Instruction.STOP or Instruction.RETURN or Instruction.REVERT or Instruction.INVALID or Instruction.SELFDESTRUCT
                        => true,
                    _ => false
                };

                if (!endCorrectly && LoggingEnabled)
                {
                    _logger.Trace($"EIP-3670 : Last opcode {opcode} in CodeSection should be either [{Instruction.STOP}, {Instruction.RETURN}, {Instruction.REVERT}, {Instruction.INVALID}, {Instruction.SELFDESTRUCT}");
                    return false;
                }

                if (spec.IsEip4200Enabled)
                {

                    foreach (int rjumpdest in rjumpdests)
                    {
                        foreach (var range in immediates)
                        {
                            if (range.Includes(rjumpdest))
                            {
                                if (LoggingEnabled)
                                {
                                    _logger.Trace($"EIP-4200 : Static Relative Jump destination {rjumpdest} is an Invalid, falls within {range}");
                                }
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }
    }
}
