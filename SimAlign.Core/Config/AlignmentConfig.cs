namespace SimAlign.Core.Config
{
    public class AlignmentConfig
    {
        public string Model { get; set; } = "bert";
        public string TokenType { get; set; } = "bpe";
        public float Distortion { get; set; } = 0.0f;
        public List<string> MatchingMethods { get; set; } = ["inter", "mwmf", "itermax", "fwd", "rev"];
        public string Device { get; set; } = "cpu";
        public int Layer { get; set; } = 8;
    }
}