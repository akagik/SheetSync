using System;

namespace SheetSync.Models
{
    [Flags]
    public enum ResultType
    {
        None = 0,
        SkipNoKey = 1 << 0,
        EmptyCell = 1 << 1,
        ConvertFails = 1 << 2,
        JoinIndexMismatch = 1 << 3,
        JoinNoReferenceRow = 1 << 4,
        JoinNoFindMethod = 1 << 5,
        VersionMismatch = 1 << 6,
        
        All = SkipNoKey | EmptyCell | ConvertFails | JoinIndexMismatch | JoinNoReferenceRow | JoinNoFindMethod | VersionMismatch
    }
}