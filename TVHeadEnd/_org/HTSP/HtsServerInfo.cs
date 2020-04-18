namespace TVHeadEnd.HTSP
{
    public class HtsServerInfo
    {
        public string Diskspace { get; internal set; }
        public string Servername { get; internal set; }
        public int ServerProtocolVersion { get; internal set; }
        public string Serverversion { get; internal set; }
        public string WebRoot { get; internal set; }
    }
}