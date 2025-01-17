namespace InfiniTicTacToe.Server.Services;

public sealed record TypedMessage(MessageType Type);

// server -> client
public sealed record ServerHelloMessage(MessageType Type = MessageType.ServerHello);

// client -> server
public sealed record ClientHelloMessage(string Nickname, MessageType Type = MessageType.ClientHello);

// client -> server
public sealed record ReadyMessage(MessageType Type = MessageType.Ready);

// server -> client
public sealed record StartMessage(PlayerSide Side, bool YourTurn, MessageType Type = MessageType.Start);

// client -> server
public sealed record MoveMessage(int X, int Y, MessageType Type = MessageType.Move);

// server -> client
public sealed record MoveResultMessage(
    bool Success,
    string Message,
    int X,
    int Y,
    int ScoreX,
    int ScoreO,
    bool YourTurn,
    MessageType Type = MessageType.MoveResult);

public sealed record GameEndMessage(int ScoreX, int ScoreO, MessageType Type = MessageType.End);

public enum MessageType
{
    ServerHello,
    ClientHello,
    Ready,
    Start,
    Move,
    MoveResult,
    End,
}

public enum PlayerSide
{
    X,
    O,
}
