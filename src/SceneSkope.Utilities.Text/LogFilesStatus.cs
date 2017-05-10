using Newtonsoft.Json;

namespace SceneSkope.Utilities.Text
{
    public class LogFilesStatus
    {
        [JsonProperty("Pattern")]
        public string Pattern { get; set; }

        [JsonProperty("CurrentName")]
        public string CurrentName { get; set; }

        [JsonProperty("Position", Required = Required.Default)]
        public long? Position { get; set; }

        [JsonProperty("LineNumber", DefaultValueHandling = DefaultValueHandling.Populate)]
        public int LineNumber { get; set; }

        [JsonProperty("Additional")]
        public string Additional { get; set; }
    }
}
