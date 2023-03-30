using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethermind.Consensus.Bor;

public class BorValidatorJsonConverter : JsonConverter<BorValidator>
{
    public override BorValidator ReadJson(JsonReader reader, Type objectType, BorValidator? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        return new BorValidator
        {
            Id = obj.GetRequired("ID").ToObjectRequired<long>(serializer),
            Address = obj.GetRequired("signer").ToObjectRequired<Address>(serializer),
            VotingPower = obj.GetRequired("power").ToObjectRequired<long>(serializer),
            ProposerPriority = obj.GetRequired("accum").ToObjectRequired<long>(serializer),
        };
    }

    public override void WriteJson(JsonWriter writer, BorValidator? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(value);

        JObject obj = new()
        {
            ["ID"] = value.Id,
            ["signer"] = JToken.FromObject(value.Address, serializer),
            ["power"] = value.VotingPower,
            ["accum"] = value.ProposerPriority
        };

        obj.WriteTo(writer);
    }
}

public class HeimdallSpanJsonConverter : JsonConverter<HeimdallSpan>
{
    public override HeimdallSpan ReadJson(JsonReader reader, Type objectType, HeimdallSpan? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        return new HeimdallSpan
        {
            Number = obj.GetRequired("span_id").ToObjectRequired<long>(serializer),
            StartBlock = obj.GetRequired("start_block").ToObjectRequired<long>(serializer),
            EndBlock = obj.GetRequired("end_block").ToObjectRequired<long>(serializer),
            ChainId = obj.GetRequired("bor_chain_id").ToObjectRequired<ulong>(serializer),
            ValidatorSet = obj.GetRequired("validator_set").ToObjectRequired<BorValidatorSet>(serializer),
            SelectedProducers = obj.GetRequired("selected_producers").ToObjectRequired<BorValidator[]>(serializer)
        };
    }

    public override void WriteJson(JsonWriter writer, HeimdallSpan? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(value);

        JObject obj = new()
        {
            ["span_id"] = value.Number,
            ["start_block"] = value.StartBlock,
            ["end_block"] = value.EndBlock,
            ["bor_chain_id"] = value.ChainId,
            ["validator_set"] = JToken.FromObject(value.ValidatorSet, serializer),
            ["selected_producers"] = JToken.FromObject(value.SelectedProducers, serializer)
        };

        obj.WriteTo(writer);
    }
}

public class StateSyncEventRecordJsonConverter : JsonConverter<StateSyncEventRecord>
{
    public override StateSyncEventRecord ReadJson(JsonReader reader, Type objectType, StateSyncEventRecord? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);

        return new StateSyncEventRecord
        {
            Id = obj.GetRequired("id").ToObjectRequired<ulong>(serializer),
            Time = obj.GetRequired("record_time").ToObjectRequired<ulong>(serializer),
            ContractAddress = obj.GetRequired("contract").ToObjectRequired<Address>(serializer),
            Data = obj.GetRequired("data").ToObjectRequired<byte[]>(serializer),
            TxHash = obj.GetRequired("tx_hash").ToObjectRequired<Keccak>(serializer),
            LogIndex = obj.GetRequired("log_index").ToObjectRequired<long>(serializer),
            ChainId = obj.GetRequired("bor_chain_id").ToObjectRequired<long>(serializer)
        };
    }

    public override void WriteJson(JsonWriter writer, StateSyncEventRecord? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(value);

        JObject obj = new()
        {
            ["id"] = value.Id,
            ["record_time"] = value.Time,
            ["contract"] = JToken.FromObject(value.ContractAddress, serializer),
            ["data"] = JToken.FromObject(value.Data, serializer),
            ["tx_hash"] = value.TxHash.ToString(),
            ["log_index"] = value.LogIndex,
            ["bor_chain_id"] = value.ChainId
        };

        obj.WriteTo(writer);
    }
}
