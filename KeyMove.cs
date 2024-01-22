using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Godot;

public class KeyMove
{
  private enum Axis
  {
    X,
    Y
  }

  private struct KeyMoveEvent
  {
    public KeyMoveEvent(Axis axis, Game.Direction direct, bool isReleased)
    {
      Axis = axis;
      Direct = direct;
      IsReleased = isReleased;
    }
    public Game.Direction Direct;
    public Axis Axis;
    public bool IsReleased;
  }

  public class KeyMoveController
  {
    private readonly Key Up;
    private readonly Key Down;
    private readonly Key Left;
    private readonly Key Right;

    // 创建一个 Subject 用于发布 KeyMoveEvent 事件
    private readonly ISubject<KeyMoveEvent> KeyMoveEventSubject = new Subject<KeyMoveEvent>();
    public readonly IObservable<int> Moves;

    public KeyMoveController(Key up, Key down, Key left, Key right)
    {
      Up = up; Down = down; Left = left; Right = right;
      Moves = TransformEventsToMoves(KeyMoveEventSubject);
    }

    public void Proxy(InputEvent @event)
    {
      if (@event is InputEventKey eventKey && !eventKey.IsEcho())
      {
        if (eventKey.Keycode == Up)
        {
          KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.Y, Game.Direction.Back, eventKey.IsReleased())); return;
        }
        if (eventKey.Keycode == Down)
        {
          KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.Y, Game.Direction.Forward, eventKey.IsReleased())); return;
        }
        if (eventKey.Keycode == Left)
        {
          KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.X, Game.Direction.Back, eventKey.IsReleased())); return;
        }
        if (eventKey.Keycode == Right)
        {
          KeyMoveEventSubject.OnNext(new KeyMoveEvent(Axis.X, Game.Direction.Forward, eventKey.IsReleased())); return;
        }
      }
    }

    static private IObservable<int> TransformEventsToMoves(ISubject<KeyMoveEvent> source)
    {
      var rawKeyEvents = source.AsObservable()
      .GroupBy(keyMoveEvent => keyMoveEvent.Axis)
      .Select(keyMoveEvents => new
      {
        keyMoveEvents.Key,
        Value = keyMoveEvents
        .Scan(Array.Empty<Game.Direction>(), (Game.Direction[] acc, KeyMoveEvent moveEvent) => moveEvent.IsReleased ?
        acc.Where(i => i != moveEvent.Direct).ToArray() :
        acc.TakeLast(1).Append(moveEvent.Direct).ToArray())
        .Select(acc => acc.LastOrDefault(Game.Direction.Still))
        .DistinctUntilChanged()
      });
      var x = rawKeyEvents.Where(e => e.Key == Axis.X).SelectMany(e => e.Value);
      var y = rawKeyEvents.Where(e => e.Key == Axis.Y).SelectMany(e => e.Value);

      var rawMoves = x.StartWith(Game.Direction.Still)
        .CombineLatest(y.StartWith(Game.Direction.Still), (x, y) => new Game.Step(x, y).Code);

      var echoedMoveSubject = new Subject<int>();
      var lastMove = 0;
      var lastMoveAt = DateTime.UtcNow;
      System.Threading.Timer timer = null;
      rawMoves.Subscribe((move) =>
      {
        var now = DateTime.UtcNow;
        var timeSpan = now - lastMoveAt;
        lastMoveAt = now;
        GD.Print("Raw ", move, ' ', timeSpan.TotalMilliseconds);

        timer?.Dispose();
        if (move != 0)
        {
          timer = new System.Threading.Timer((_) =>
          {
            echoedMoveSubject.OnNext(move);
          }, null, 90, 100); // dueTime 需要比 period 短一点，否则后面 Sample 的时候容易漏拍
        }

        // 去掉多个按键组合时产生的中间态 Move，比如 ↗️(→+↑) 之前会先触发一次 →
        // 只需关注 0 之后的情况，其他情况（比如释放时的中间态）会被之后的 Sample 过滤掉
        if (lastMove == 0 && new int[] {
      -1, 1, -3, 3
      }.Contains(move))
        {
          Task.Delay(20).ContinueWith((task) =>
          {
            if (lastMoveAt == now)
            {
              echoedMoveSubject.OnNext(move);
              lastMove = move;
            }
          });
        }
        else
        {
          if (move == 0)
          {
            // 如果遇到 0 → 0 这种情况，在遇到后一个 0 时补上上一个被过滤掉的 →
            echoedMoveSubject.OnNext(lastMove);
          }
          echoedMoveSubject.OnNext(move);
        }
        lastMove = move;
      });

      return echoedMoveSubject;
    }
  }
}