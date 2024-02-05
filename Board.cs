using Godot;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using static KeyMove;

public partial class Board : Node2D
{
  private const int Size = 13; // 棋盘大小
  private const int CellSize = 46; // 每个单元格的像素大小
  private const int BorderSize = 2; // 边框大小
  private const int ViewportWidth = 626;

  private static Color ColorA = new(0xa9dbdeff);
  private static Color ColorB = new(0xfad0b1ff);
  private static Color ColorBlank = Colors.White;

  private static Color[] PlayerColors = new Color[] {
    new(0xfaa66cff),
    new(0x72d8ddff),
    new(0xe98585ff),
    new(0x74a1efff),
    new(0xffcc66ff),
    new(0x97df8bff),
  };

  private const int PlayerCount = 2;
  private readonly Game game = new(Size, PlayerCount);

  private static ColorRect[] cells = new ColorRect[Size * Size];

  private readonly KeyMoveController keyMoveController1 = new(Key.W, Key.S, Key.A, Key.D);
  private readonly KeyMoveController keyMoveController2 = new(Key.Up, Key.Down, Key.Left, Key.Right);

  // Called when the node enters the scene tree for the first time.
  public override void _Ready()
  {
    GenerateBoard();

    BindNodes();

    game.Bind(new IObservable<int>[] {
      keyMoveController1.Moves,
        keyMoveController2.Moves,
    }).Select((moveStream, i) => moveStream.FirstAsync().Subscribe(_ => CallDeferred("HideHint", i))).ToArray();
  }

  private async void Start()
  {
    GD.Print("Game start");

    StartBtn.Visible = false;
    StartBtn.Text = "Restart";

    // 3 2 1 59 58 57 ... 1 0
    var CountDownNumbers = Observable
      .Interval(TimeSpan.FromSeconds(1))
      .Take(Game.StandbyDurationSeconds - 1)
      .Select(i => Game.StandbyDurationSeconds - i - 1)
      .StartWith(Game.StandbyDurationSeconds)
      .Concat(Observable
        .Interval(TimeSpan.FromSeconds(1))
        .Take(Game.DurationSeconds + 1)
        .Select(i => Game.DurationSeconds - i)
      );
    CountDownNumbers.Subscribe(i => CallDeferred("UpdateCountDown", i));

    game.Reset();
    await game.Start();

    StartBtn.SetDeferred("visible", true);
  }

  private void HideHint(int i)
  {
    var hint = GetNode<Control>($"Container/Board/CtrHints{i}");
    if (hint != null)
    {
      hint.Visible = false;
    }
  }

  private void UpdateCountDown(long i)
  {
    TimerNode.Text = i.ToString();
  }

  private Label TimerNode;
  private Label Team1ScoreNode;
  private Label Team2ScoreNode;
  private Control Team1ScoreBarNode;
  private Control Team2ScoreBarNode;
  private Button StartBtn;
  private void BindNodes()
  {
    TimerNode = GetNode<Label>("Container/Timer");
    Team1ScoreBarNode = GetNode<Control>("Container/ScoreBoard/Team1");
    Team2ScoreBarNode = GetNode<Control>("Container/ScoreBoard/Team2");
    Team1ScoreNode = GetNode<Label>("Container/ScoreBoard/Team1/Bar/Score");
    Team2ScoreNode = GetNode<Label>("Container/ScoreBoard/Team2/Bar/Score");
    StartBtn = GetNode<Button>("Container/Board/StartBtn");

    StartBtn.Pressed += Start;
  }

  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(double delta)
  {
    Paint(game);
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
      Color = new Color(1, 1, 1, (float)0.6)
    };
    var boardNode = GetNode<Control>("Container/Board/Cells");

    boardNode.AddChild(background);

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
        boardNode.AddChild(cell); // 将cell作为子节点添加到棋盘节点
      }
    }
  }

  private static Color[] CellColors = new Color[] {
    ColorA,
    ColorBlank,
    ColorB
  }.Concat(PlayerColors).ToArray();
  private int[] previousStatus = new int[Size * Size];

  private void Paint(Game game)
  {
    Team1ScoreBarNode.Size = new Vector2((ViewportWidth - 60) * game.Team1Score / (Size * Size) + 30, Team1ScoreBarNode.Size.Y);
    Team1ScoreNode.Text = game.Team1Score.ToString();
    Team2ScoreBarNode.Size = new Vector2((ViewportWidth - 60) * game.Team2Score / (Size * Size) + 30, Team2ScoreBarNode.Size.Y);
    Team2ScoreBarNode.Position = new Vector2(ViewportWidth - Team2ScoreBarNode.Size.X, Team2ScoreBarNode.Position.Y);
    Team2ScoreNode.Text = game.Team2Score.ToString();

    var currentStatus = (int[])game.Occupations.Clone();
    for (int i = 0; i < game.PlayerPositions.Length; i++)
    {
      // TODO: 如果 P1 P2 在同一个位置，展示时 P2 会稳定覆盖 P1，而非按照进入的先后（染色是对的）。可能需要一个更好的展示方式。
      currentStatus[game.PlayerPositions[i]] = i + 2;
    }
    for (int i = 0; i < game.Occupations.Length; i++)
    {
      if (previousStatus[i] != currentStatus[i])
      {
        cells[i].Color = CellColors[currentStatus[i] + 1];
      }
    }
    previousStatus = currentStatus;
  }
}
