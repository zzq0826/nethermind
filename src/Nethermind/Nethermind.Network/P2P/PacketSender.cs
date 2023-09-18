// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Nethermind.Logging;
using Nethermind.Network.P2P.Messages;

namespace Nethermind.Network.P2P
{
    public class PacketSender : ChannelHandlerAdapter, IPacketSender
    {
        private readonly IMessageSerializationService _messageSerializationService;
        private readonly ILogger _logger;
        private IChannelHandlerContext _context;
        private TimeSpan _sendLatency;

        private Channel<IByteBuffer> channel;



        public PacketSender(IMessageSerializationService messageSerializationService, ILogManager logManager,
            TimeSpan sendLatency)
        {
            _messageSerializationService = messageSerializationService ?? throw new ArgumentNullException(nameof(messageSerializationService));
            _logger = logManager.GetClassLogger<PacketSender>() ?? throw new ArgumentNullException(nameof(logManager));
            _sendLatency = sendLatency;

            channel = Channel.CreateBounded<IByteBuffer>(1);

        }

        public int Enqueue<T>(T message) where T : P2PMessage
        {
            if (!_context.Channel.Active)
            {
                return 0;
            }

            IByteBuffer buffer = _messageSerializationService.ZeroSerialize(message);
            int length = buffer.ReadableBytes;

            channel.Writer.WriteAsync(buffer);

            // Running in background
            _ = SendBuffer(buffer);

            return length;
        }

        private async Task SendBuffer(IByteBuffer buffer)
        {
            try
            {
                if (_sendLatency != TimeSpan.Zero)
                {
                    // Tried to implement this as a pipeline handler. Got a lot of peering issue for some reason...
                    await Task.Delay(_sendLatency);
                }

                // await _context.WriteAndFlushAsync(buffer);
                await _context.Executor.SubmitAsync(() => _context.WriteAndFlushAsync(buffer));
            }
            catch (Exception exception)
            {
                if (_context.Channel is { Active: false })
                {
                    if (_logger.IsTrace) _logger.Trace($"Channel is not active - {exception.Message}");
                }
                else if (_logger.IsError) _logger.Error("Channel is active", exception);
            }
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            _context = context;

            _ = Task.Run(async () =>
            {
                while (await channel.Reader.WaitToReadAsync())
                {
                    var buffer = await channel.Reader.ReadAsync();
                    await SendBuffer(buffer);
                }
            });
        }
    }
}
