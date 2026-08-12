using Notify.Core.Models.Yaml;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Notify.Helper
{
    public sealed class MessageStepFormatter : IYamlFormatter<MessageStepConfig>
    {
        public static readonly MessageStepFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, MessageStepConfig? value, YamlSerializationContext context)
        {
            if (value is null)
            {
                emitter.WriteNull();
                return;
            }
            context.Serialize(ref emitter, value.Variants);
        }

        public MessageStepConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            var stepConfig = new MessageStepConfig();

            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                return stepConfig;
            }

            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();

                if (key == "text" || key == "subject" || key == "button_text" || key == "experiment")
                {
                    var variant = context.DeserializeWithAlias<MessageVariantConfig>(ref parser);
                    stepConfig.Variants["default"] = variant;
                    break;
                }

                var variantConfig = context.DeserializeWithAlias<MessageVariantConfig>(ref parser);
                if (key != null && variantConfig != null)
                {
                    stepConfig.Variants[key] = variantConfig;
                }
            }

            if (parser.CurrentEventType == ParseEventType.MappingEnd)
            {
                parser.Read();
            }

            return stepConfig;
        }
    }
}