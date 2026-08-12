using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Notify.Helper
{
    public sealed class FlexibleStringListFormatter : IYamlFormatter<List<string>>
    {
        public static readonly FlexibleStringListFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, List<string>? value, YamlSerializationContext context)
        {
            if (value is null)
            {
                emitter.WriteNull();
                return;
            }

            emitter.BeginSequence();
            foreach (var item in value)
            {
                emitter.WriteString(item);
            }
            emitter.EndSequence();
        }

        public List<string> Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            var list = new List<string>();

            if (parser.CurrentEventType == ParseEventType.Scalar)
            {
                var value = parser.ReadScalarAsString();
                if (!string.IsNullOrEmpty(value))
                {
                    list.Add(value);
                }
            }
            else if (parser.CurrentEventType == ParseEventType.SequenceStart)
            {
                parser.Read();
                while (parser.CurrentEventType != ParseEventType.SequenceEnd)
                {
                    var value = parser.ReadScalarAsString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        list.Add(value);
                    }
                }
                parser.Read();
            }

            return list;
        }
    }
}