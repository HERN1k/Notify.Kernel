using Notify.Core.Models.Yaml;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Notify.Helper
{
    public static class YamlOptionsFactory
    {
        public static YamlSerializerOptions CreateOptions()
        {
            return new YamlSerializerOptions
            {
                Resolver = CompositeResolver.Create(
                    new IYamlFormatter[]
                    {
                        WorkflowRootConfigFormatter.Instance,
                        EventConfigFormatter.Instance,
                        WorkflowDefinitionFormatter.Instance,
                        TransitionConfigFormatter.Instance,
                        ScheduleStepConfigFormatter.Instance,
                        ConditionConfigFormatter.Instance,
                        MessageStepFormatter.Instance,
                        MessageVariantConfigFormatter.Instance,
                        FlexibleStringListFormatter.Instance
                    },
                    new IYamlFormatterResolver[]
                    {
                        BuiltinResolver.Instance
                    }
                )
            };
        }

        #region Helper Methods for Dictionaries
        public static Dictionary<string, string> ReadStringDictionary(ref YamlParser parser)
        {
            var dict = new Dictionary<string, string>();
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return dict;
            }

            parser.Read(); // Consume MappingStart
            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString() ?? string.Empty;
                var val = parser.ReadScalarAsString() ?? string.Empty;
                dict[key] = val;
            }
            parser.Read(); // Consume MappingEnd
            return dict;
        }

        public static Dictionary<string, T> ReadDictionary<T>(ref YamlParser parser, YamlDeserializationContext context)
        {
            var dict = new Dictionary<string, T>();
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return dict;
            }

            parser.Read(); // Consume MappingStart
            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString() ?? string.Empty;
                var val = context.DeserializeWithAlias<T>(ref parser);
                if (val != null)
                {
                    dict[key] = val;
                }
            }
            parser.Read(); // Consume MappingEnd
            return dict;
        }
        #endregion
    }

    #region FlexibleStringListFormatter
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
    #endregion

    #region MessageStepFormatter
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
    #endregion

    #region 1. EventConfigFormatter
    public class EventConfigFormatter : IYamlFormatter<EventConfig>
    {
        public static readonly EventConfigFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, EventConfig value, YamlSerializationContext context) => throw new NotImplementedException();

        public EventConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new EventConfig();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "code":
                        result.Code = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "action":
                        result.Action = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "triggers":
                        result.Triggers = YamlOptionsFactory.ReadStringDictionary(ref parser);
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion

    #region 2. TransitionConfigFormatter
    public class TransitionConfigFormatter : IYamlFormatter<TransitionConfig>
    {
        public static readonly TransitionConfigFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, TransitionConfig value, YamlSerializationContext context) => throw new NotImplementedException();

        public TransitionConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new TransitionConfig();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "from":
                        result.From = context.DeserializeWithAlias<List<string>>(ref parser) ?? new();
                        break;
                    case "to":
                        result.To = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion

    #region 3. WorkflowDefinitionFormatter
    public class WorkflowDefinitionFormatter : IYamlFormatter<WorkflowDefinition>
    {
        public static readonly WorkflowDefinitionFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, WorkflowDefinition value, YamlSerializationContext context) => throw new NotImplementedException();

        public WorkflowDefinition Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new WorkflowDefinition();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "type":
                        result.Type = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "places":
                        result.Places = context.DeserializeWithAlias<List<string>>(ref parser) ?? new();
                        break;
                    case "transitions":
                        result.Transitions = YamlOptionsFactory.ReadDictionary<TransitionConfig>(ref parser, context);
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion

    #region 4. ScheduleStepConfigFormatter
    public class ScheduleStepConfigFormatter : IYamlFormatter<ScheduleStepConfig>
    {
        public static readonly ScheduleStepConfigFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, ScheduleStepConfig value, YamlSerializationContext context) => throw new NotImplementedException();

        public ScheduleStepConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new ScheduleStepConfig();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "modify":
                        result.Modify = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "time":
                        result.Time = parser.ReadScalarAsString();
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion

    #region 5. ConditionConfigFormatter
    public class ConditionConfigFormatter : IYamlFormatter<ConditionConfig>
    {
        public static readonly ConditionConfigFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, ConditionConfig value, YamlSerializationContext context) => throw new NotImplementedException();

        public ConditionConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new ConditionConfig();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "query":
                        result.Query = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "min_count":
                        var minCountStr = parser.ReadScalarAsString();
                        _ = int.TryParse(minCountStr, out var minCount);
                        result.MinCount = minCount;
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion

    #region 6. MessageVariantConfigFormatter
    public class MessageVariantConfigFormatter : IYamlFormatter<MessageVariantConfig>
    {
        public static readonly MessageVariantConfigFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, MessageVariantConfig value, YamlSerializationContext context) => throw new NotImplementedException();

        public MessageVariantConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new MessageVariantConfig();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "experiment":
                        result.Experiment = parser.ReadScalarAsString();
                        break;
                    case "hypothesis":
                        result.Hypothesis = parser.ReadScalarAsString();
                        break;
                    case "subject":
                        result.Subject = YamlOptionsFactory.ReadStringDictionary(ref parser);
                        break;
                    case "text":
                        result.Text = YamlOptionsFactory.ReadStringDictionary(ref parser);
                        break;
                    case "button_text":
                        result.ButtonText = YamlOptionsFactory.ReadStringDictionary(ref parser);
                        break;
                    case "button_url":
                        result.ButtonUrl = parser.ReadScalarAsString();
                        break;
                    case "image_url":
                        result.ImageUrl = YamlOptionsFactory.ReadStringDictionary(ref parser);
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion

    #region 7. WorkflowRootConfigFormatter
    public class WorkflowRootConfigFormatter : IYamlFormatter<WorkflowRootConfig>
    {
        public static readonly WorkflowRootConfigFormatter Instance = new();

        public void Serialize(ref Utf8YamlEmitter emitter, WorkflowRootConfig value, YamlSerializationContext context) => throw new NotImplementedException();

        public WorkflowRootConfig Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        {
            if (parser.CurrentEventType != ParseEventType.MappingStart)
            {
                parser.SkipCurrentNode();
                return null!;
            }

            var result = new WorkflowRootConfig();
            parser.Read();

            while (parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                var key = parser.ReadScalarAsString();
                switch (key)
                {
                    case "name":
                        result.Name = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "enabled":
                        var enabledStr = parser.ReadScalarAsString();
                        _ = bool.TryParse(enabledStr, out var enabled);
                        result.Enabled = enabled;
                        break;
                    case "provider":
                        result.Provider = parser.ReadScalarAsString() ?? string.Empty;
                        break;
                    case "batch_limit":
                        var batchStr = parser.ReadScalarAsString();
                        _ = int.TryParse(batchStr, out var batchLimit);
                        result.BatchLimit = batchLimit;
                        break;
                    case "chunk_size":
                        var chunkSizeStr = parser.ReadScalarAsString();
                        _ = int.TryParse(chunkSizeStr, out var chunkSize);
                        result.ChunkSize = chunkSize;
                        break;
                    case "chunk_delay":
                        var chunkDelayStr = parser.ReadScalarAsString();
                        _ = int.TryParse(chunkDelayStr, out var chunkDelay);
                        result.ChunkDelay = chunkDelay;
                        break;
                    case "events":
                        result.Events = context.DeserializeWithAlias<EventConfig>(ref parser);
                        break;
                    case "workflow":
                        result.Workflow = context.DeserializeWithAlias<WorkflowDefinition>(ref parser) ?? new();
                        break;
                    case "schedule":
                        result.Schedule = YamlOptionsFactory.ReadDictionary<ScheduleStepConfig>(ref parser, context);
                        break;
                    case "conditions":
                        result.Conditions = YamlOptionsFactory.ReadDictionary<ConditionConfig>(ref parser, context);
                        break;
                    case "message":
                        result.Message = YamlOptionsFactory.ReadDictionary<MessageStepConfig>(ref parser, context);
                        break;
                    case "callbacks":
                        result.Callbacks = YamlOptionsFactory.ReadStringDictionary(ref parser);
                        break;
                    case "subject":
                        result.Subject = parser.ReadScalarAsString();
                        break;
                    default:
                        parser.SkipCurrentNode();
                        break;
                }
            }

            parser.Read();
            return result;
        }
    }
    #endregion
}