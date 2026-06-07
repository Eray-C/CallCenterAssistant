namespace CallCenterAssistant.Models.Request
{
    public class OriginateRequest
    {
        public string Channel { get; set; } = string.Empty; // e.g. "PJSIP/100" or "SIP/100"
        public string Exten { get; set; } = string.Empty;   // Target number/extension, e.g. "101"
        public string Context { get; set; } = "from-internal";
        public int Priority { get; set; } = 1;
        public string CallerId { get; set; } = "Call Center Assistant";
        public int Timeout { get; set; } = 30000; // default 30 seconds
    }
}
