using Godot;
using System;
using System.Reactive.Linq;
using static KeyMove;

public partial class Board : Node2D
{
  private
  const int Size = 13; // 棋盘大小
  private
  const int CellSize = 46; // 每个单元格的像素大小
  private
  const int BorderSize = 2; // 边框大小

  private Color ColorA = new(0xfaa66cff);
  private Color ColorB = new(0x72d8ddff);

  private readonly Game game = new(Size, 2);

  private ColorRect[] cells = new ColorRect[Size * Size];

  private readonly KeyMoveController keyMoveController1 = new(Key.W, Key.S, Key.A, Key.D);
  private readonly KeyMoveController keyMoveController2 = new(Key.Up, Key.Down, Key.Left, Key.Right);

  // Called when the node enters the scene tree for the first time.
  public override void _Ready()
  {
    GenerateBoard();

    keyMoveController1.Moves.Subscribe(move => game.Move(0, move));
    keyMoveController2.Moves.Subscribe(move => game.Move(1, move));
  }

  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(double delta)
  {
    Paint();
  }

  public override void _Input(InputEvent @event)
  {
    keyMoveController1.Proxy(@event);
    keyMoveController2.Proxy(@event);
  }

  private void GenerateBoard()
  {
    var boardWidth = (CellSize + BorderSize) * Size + BorderSize;
    var background = new ColorRect
    {
      Size = new Vector2(boardWidth, boardWidth),
      Color = new Color(1, 1, 1, (float)0.75)
    };
    AddChild(background);

    for (int i = 0; i < Size; i++)
    {
      for (int j = 0; j < Size; j++)
      {
        var cell = new ColorRect
        {
          Size = new Vector2(CellSize, CellSize),
          Color = Colors.White,
          Position = new Vector2(j * (CellSize + BorderSize) + BorderSize, i * (CellSize + BorderSize) + BorderSize)
        };
        cells[i * Size + j] = cell;
        AddChild(cell); // 将cell作为子节点添加到棋盘节点
      }
    }
  }

  private int[] previousStatus = new int[Size * Size];
  private void Paint()
  {
    for (int i = 0; i < game.Status.Length; i++)
    {
      var currentStatus = game.Status[i];
      if (previousStatus[i] != game.Status[i])
      {
        cells[i].Color = currentStatus == 0 ? Colors.White : currentStatus == 1 ? ColorA : ColorB;
      }
    }
    previousStatus = (int[])game.Status.Clone();
  }
}
