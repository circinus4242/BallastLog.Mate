namespace BallastLog.Mate.Models;

public enum OpType { B, DB, TR, MISC }
public enum LegDir { From = -1, To = 1 }

public enum OpState
{
    Ok = 0,
    InvalidTotals = 1,
    Overflow = 2,
    Underflow = 3,
    FlowExceeded = 4
}