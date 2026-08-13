using VYaml.Annotations;

namespace Notify.Core.Models.Yaml
{
    [YamlObject]
    public partial class WorkflowRootConfig
    {
        [YamlMember("name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember("enabled")]
        public bool Enabled { get; set; }

        [YamlMember("provider")]
        public string Provider { get; set; } = string.Empty;

        [YamlMember("batch_limit")]
        public int BatchLimit { get; set; }

        [YamlMember("chunk_size")]
        public int ChunkSize { get; set; }

        [YamlMember("chunk_delay")]
        public int ChunkDelay { get; set; }

        [YamlMember("events")]
        public EventConfig? Events { get; set; }

        [YamlMember("workflow")]
        public WorkflowDefinition Workflow { get; set; } = new();

        [YamlMember("schedule")]
        public Dictionary<int, ScheduleStepConfig> Schedule { get; set; } = new();

        [YamlMember("conditions")]
        public Dictionary<string, ConditionConfig> Conditions { get; set; } = new();

        [YamlMember("message")]
        public Dictionary<int, MessageStepConfig> Message { get; set; } = new();

        [YamlMember("callbacks")]
        public Dictionary<string, string>? Callbacks { get; set; }

        [YamlMember("subject")]
        public string? Subject { get; set; }
    }

    [YamlObject]
    public partial class EventConfig
    {
        [YamlMember("code")]
        public string Code { get; set; } = string.Empty;

        [YamlMember("action")]
        public string Action { get; set; } = string.Empty;

        [YamlMember("triggers")]
        public Dictionary<int, string> Triggers { get; set; } = new();
    }

    [YamlObject]
    public partial class WorkflowDefinition
    {
        [YamlMember("type")]
        public string Type { get; set; } = string.Empty;

        [YamlMember("places")]
        public List<string> Places { get; set; } = new();

        [YamlMember("transitions")]
        public Dictionary<string, TransitionConfig> Transitions { get; set; } = new();
    }

    [YamlObject]
    public partial class TransitionConfig
    {
        [YamlMember("from")]
        public List<string> From { get; set; } = new();

        [YamlMember("to")]
        public string To { get; set; } = string.Empty;
    }

    [YamlObject]
    public partial class ScheduleStepConfig
    {
        [YamlMember("modify")]
        public string Modify { get; set; } = string.Empty;

        [YamlMember("time")]
        public string? Time { get; set; }
    }

    [YamlObject]
    public partial class ConditionConfig
    {
        [YamlMember("query")]
        public string Query { get; set; } = string.Empty;

        [YamlMember("min_count")]
        public int MinCount { get; set; }
    }

    [YamlObject]
    public partial class MessageStepConfig
    {
        public Dictionary<string, MessageVariantConfig> Variants { get; set; } = new();
    }

    [YamlObject]
    public partial class MessageVariantConfig
    {
        [YamlMember("experiment")]
        public string? Experiment { get; set; }

        [YamlMember("hypothesis")]
        public string? Hypothesis { get; set; }

        [YamlMember("subject")]
        public Dictionary<string, string?>? Subject { get; set; }

        [YamlMember("text")]
        public Dictionary<string, string?> Text { get; set; } = new();

        [YamlMember("button_text")]
        public Dictionary<string, string?>? ButtonText { get; set; }

        [YamlMember("button_url")]
        public string? ButtonUrl { get; set; }

        [YamlMember("image_url")]
        public Dictionary<string, string?>? ImageUrl { get; set; }
    }
}