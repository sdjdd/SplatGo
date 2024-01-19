using Godot;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

public partial class Board: Node2D {

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

  // Called when the node enters the scene tree for the first time.
  public override void _Ready() {
    GenerateBoard();
    Paint();
    var rawKeyEvents = KeyMoveEventSubject.AsObservable()
      .GroupBy(keyMoveEvent => keyMoveEvent.Axis)
      .Select(keyMoveEvents => new {
        keyMoveEvents.Key,
          Value = keyMoveEvents
          .Scan(Array.Empty < Game.Direction > (), (Game.Direction[] acc, KeyMoveEvent moveEvent) => moveEvent.IsReleased ?
            acc.Where(i => i != moveEvent.Direct).ToArray() :
            acc.TakeLast(1).Append(moveEvent.Direct).ToArray())
          .Select(acc => acc.LastOrDefault(Game.Direction.Still))
          .DistinctUntilChanged()
      });
    var x = rawKeyEvents.Where(e => e.Key == Axis.X).SelectMany(e => e.Value);
    var y = rawKeyEvents.Where(e => e.Key == Axis.Y).SelectMany(e => e.Value);

    var rawMoves = x.StartWith(Game.Direction.Still)
      .CombineLatest(y.StartWith(Game.Direction.Still), (x, y) => new Game.Step(x, y).Code);

    var echoedMoveSubject = new Subject < int > ();
    var lastMove = 0;
    var lastMoveAt = DateTime.UtcNow;
    System.Threading.Timer timer = null;
    rawMoves.Subscribe((move) => {
      var now = DateTime.UtcNow;
      var timeSpan = now - lastMoveAt;
      lastMoveAt = now;
      GD.Print("Raw ", move, ' ', timeSpan.TotalMilliseconds);

      timer?.Dispose();
      if (move != 0) {
        timer = new System.Threading.Timer((_) => {
          echoedMoveSubject.OnNext(move);
        }, null, 490, 500); // dueTime 需要比 period 短一点，否则后面 Sample 的时候容易漏拍
      }

      // 去掉多个按键组合时产生的中间态 Move，比如 ↗️(→+↑) 之前会先触发一次 →
      if (lastMove == 0 && new int[] {
          -1, 1, -3, 3
        }.Contains(move)) {
        Task.Delay(20).ContinueWith((task) => {
          if (lastMoveAt == now) {
            echoedMoveSubject.OnNext(move);
            lastMove = move;
          }
        });
      } else {
        if (move == 0) {
          echoedMoveSubject.OnNext(lastMove);
        }
        echoedMoveSubject.OnNext(move);
        lastMove = move;

      }
    });

    var echoedMoves = echoedMoveSubject.AsObservable();
    echoedMoves.Subscribe(m => GD.Print(m));

    var sampledMoves = Observable.Create < int > (observer =>
        echoedMoves.Sample(echoedMoves.SkipWhile(move => move == 0).FirstAsync().Concat(
          Observable.Interval(TimeSpan.FromMilliseconds(500)).Select(_ => (int) 1))).TakeUntil(move => move == 0).Subscribe(
          onNext: observer.OnNext,
          onError: observer.OnError,
          onCompleted: observer.OnCompleted)
      )
      .Repeat() // 当序列完成时（即遇到了0），使用Repeat操作符重新订阅源序列
    ;

    sampledMoves.Subscribe(ProcessMove);
  }

  private enum Axis {
    X,
    Y
  }

  private struct KeyMoveEvent {
    public KeyMoveEvent(Axis axis, Game.Direction direct, bool isReleased) {
      Axis = axis;
      Direct = direct;
      IsReleased = isReleased;
    }
    public Game.Direction Direct;
    public Axis Axis;
    public bool IsReleased;
  }

  private DateTime ts = DateTime.UtcNow;
  private void ProcessMove(int direct) {
    var y = (direct + 4) / 3 - 1;
    var x = direct - y * 3;
    var now = DateTime.UtcNow;
    GD.Print("Move ", direct, y, x, ' ', (now - ts).TotalMilliseconds);
    ts = now;
    game.Move(0, direct);
  }

  // Called every frame. 'delta' is the elapsed time since the previous frame.
  public override void _Process(double delta) {
    Paint();
  }

  // 创建一个 Subject 用于发布 KeyMoveEvent 事件
  private readonly ISubject < KeyMoveEvent > KeyMoveEventSubject = new Subject < KeyMoveEvent > ();

  public override void _Input(InputEvent @event) {
    if (@event is InputEventKey eventKey && !eventKey.IsEcho()) {
      switch (eventKey.Keycode) {
      case Key.Up:
        KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.Y, Game.Direction.Back, eventKey.IsReleased()));
        break;
      case Key.Down:
        KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.Y, Game.Direction.Forward, eventKey.IsReleased()));
        break;
      case Key.Left:
        KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.X, Game.Direction.Back, eventKey.IsReleased()));
        break;
      case Key.Right:
        KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.X, Game.Direction.Forward, eventKey.IsReleased()));
        break;
      }
    }
  }

  private void GenerateBoard() {
    var boardWidth = (CellSize + BorderSize) * Size + BorderSize;
    var background = new ColorRect {
      Size = new Vector2(boardWidth, boardWidth),
        Color = new Color(1, 1, 1, (float) 0.75)
    };
    AddChild(background);

    for (int i = 0; i < Size; i++) {
      for (int j = 0; j < Size; j++) {
        var cell = new ColorRect {
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
  private void Paint() {
    for (int i = 0; i < game.Status.Length; i++) {
      var currentStatus = game.Status[i];
      if (previousStatus[i] != game.Status[i]) {
        cells[i].Color = currentStatus == 0 ? Colors.White : currentStatus == 1 ? ColorA : ColorB;
      }
    }
    previousStatus = (int[]) game.Status.Clone();
  }
}