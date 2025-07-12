namespace SheetSync
{
    /// <summary>
    /// ダウンロード対象のシート情報
    /// </summary>
    public class SheetDownloadInfo
    {
        public string TargetPath { get; set; }
        public string SheetId { get; set; }
        public string Gid { get; set; }
        
        public SheetDownloadInfo(string targetPath, string sheetId, string gid)
        {
            TargetPath = targetPath;
            SheetId = sheetId;
            Gid = gid;
        }
    }
}