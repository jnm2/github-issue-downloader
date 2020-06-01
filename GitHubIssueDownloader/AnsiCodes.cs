namespace GitHubIssueDownloader
{
    internal static class AnsiCodes
    {
        public const string ControlSequenceIntroducer = "\u001b[";

        public const string CursorHorizontalAbsolute1 = ControlSequenceIntroducer + "G";

        public const string EraseLineFromCursorToEnd = ControlSequenceIntroducer + "K";
    }
}
