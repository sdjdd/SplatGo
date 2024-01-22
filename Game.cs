using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

public class Game
{
  public int Size
  {
    get;
    private set;
  }
  public static TimeSpan Duration = TimeSpan.FromSeconds(30);
  public static TimeSpan CountDownDuration = TimeSpan.FromSeconds(3);
  public static TimeSpan StepInterval = TimeSpan.FromMilliseconds(500);
  public enum GameStatus
  {
    Initial,
    Counting,
    Ongoing,
    Ended,
  }
  private GameStatus status;
  public GameStatus Status
  {
    get
    {
      return status;
    }
    set
    {
      status = value;
      GameStatusSubject.OnNext(value);
    }
  }
  public int[] Occupations;
  public int[] PlayerPositions;
  private ISubject<GameStatus> GameStatusSubject = new Subject<GameStatus>();
  public Game(int size, int playerCount)
  {
    Size = size;
    Occupations = new int[size * size];
    Array.Fill(Occupations, 0);
    PlayerPositions = new int[playerCount];
    PlayerPositions[0] = Occupations.Length / 2 - 3;
    PlayerPositions[1] = Occupations.Length / 2 + 3;
    for (int i = 0; i < playerCount; i++)
    {
      UpdateOccupation(i, PlayerPositions[i]);
    }
    Status = GameStatus.Initial;
  }

  public void Bind(IObservable<int>[] inputStreams)
  {
    var startStream = GameStatusSubject.Where(status => status == GameStatus.Ongoing).Take(1);
    inputStreams.Select((inputStream, i) =>
    {
      Subject<int> trimmedInput = new();
      inputStream.CombineLatest(startStream, (input, start) => input).Subscribe(trimmedInput);
      var sampledMoves = Observable.Create<int>(observer =>
          trimmedInput.Sample(
            trimmedInput
            .SkipWhile(move => move == 0)
            .FirstAsync()
            .Concat(Observable.Interval(TimeSpan.FromMilliseconds(500)).Select(_ => (int)1))
          ).TakeUntil(move => move == 0).Subscribe(observer))
        .Repeat(); // 当序列完成时（即遇到了0），使用Repeat操作符重新订阅源序列
      return sampledMoves.Subscribe(move => Move(i, move));
    }).ToArray();
  }

  public async void Start()
  {
    Status = GameStatus.Counting;
    await Task.Delay(CountDownDuration);
    Status = GameStatus.Ongoing;
    await Task.Delay(Duration);
    Status = GameStatus.Ended;
    CheckVictory();
  }

  public enum Direction
  {
    Back = -1,
    Still = 0,
    Forward = 1,
  }

  public class Step
  {
    public Direction X;
    public Direction Y;

    // -4 | -3 | -2 |
    // -1 |  0 |  1 |
    //  2 |  3 |  4
    public int Code;
    public Step(Direction x, Direction y)
    {
      Y = y;
      X = x;
      Code = (int)x + 3 * (int)y;
    }
    public Step(int code)
    {
      Code = code;
      var y = (code + 4) / 3 - 1;
      var x = code - (y * 3);
      Y = (Direction)y;
      X = (Direction)x;
    }
  }

  private bool Move(int playerId, int code)
  {
    var step = new Step(code);
    return Move(playerId, step.X, step.Y);
  }
  private bool Move(int playerId, Direction x, Direction y)
  {
    if (Status != GameStatus.Ongoing)
    {
      return false;
    }
    var previousPosition = PlayerPositions[playerId];
    // TODO: implement boundry
    var newPosition = (PlayerPositions[playerId] + (int)x + (int)y * Size + Size * Size) % (Size * Size);
    if (previousPosition == newPosition)
    {
      return false;
    }
    PlayerPositions[playerId] = newPosition;
    var occupationChanges = UpdateOccupation(playerId, newPosition);
    if (occupationChanges)
    {
      FillClosedArea(newPosition);
      CheckVictory();
    }
    return true;
  }

  private bool UpdateOccupation(int playerId, int position)
  {
    var previousOccupation = Occupations[position];
    var newOccupation = playerId % 2 == 0 ? 1 : -1;
    Occupations[position] = newOccupation;
    return previousOccupation != newOccupation;
  }

  private void FillClosedArea(int startPoint)
  {
    // TODO: implement FillClosedArea
  }

  private void CheckVictory()
  {
    // TODO: implement 胜利条件
  }

}