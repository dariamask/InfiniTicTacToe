namespace InfiniTicTacToe.Server.Models;

public sealed record Cell(int X, int Y, Side Side)
{
    public int X { get; init; } = X;

    public int Y { get; init; } = Y;

    public Side Side { get; init; } = Side;

    public int? TurnNumber { get; set; }

    public bool CrossedOut { get; set; }

    public char Symbol => Side switch
    {
        Side.X => 'X',
        Side.O => 'O',
        _ => ' ',
    };
}
