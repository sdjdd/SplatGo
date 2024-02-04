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
  public byte PlayersCount
  {
    get;
    private set;
  }
  public static int DurationSeconds = 60;
  public static TimeSpan Duration = TimeSpan.FromSeconds(DurationSeconds);
  public static int StandbyDurationSeconds = 3;
  public static TimeSpan StandbyDuration = TimeSpan.FromSeconds(StandbyDurationSeconds);
  public static TimeSpan StepInterval = TimeSpan.FromMilliseconds(500);
  public enum GameStatus
  {
    Initial,
    Standby,
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
  public int Team1Score = 0;
  public int Team2Score = 0;
  private ISubject<GameStatus> GameStatusSubject = new Subject<GameStatus>();
  public Game(int size, byte playerCount)
  {
    Size = size;
    PlayersCount = playerCount;
    Occupations = new int[size * size];
    Reset();
  }

  public void Reset()
  {
    Array.Fill(Occupations, 0);
    PlayerPositions = new int[PlayersCount];
    PlayerPositions[0] = Occupations.Length / 2 - 3;
    PlayerPositions[1] = Occupations.Length / 2 + 3;
    for (int i = 0; i < PlayersCount; i++)
    {
      UpdateOccupation(i, PlayerPositions[i]);
    }
    Status = GameStatus.Initial;
  }

  public IObservable<int>[] Bind(IObservable<int>[] inputStreams)
  {
    var startStream = GameStatusSubject.Where(status => status == GameStatus.Ongoing).Take(1);
    return inputStreams.Select((inputStream, i) =>
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
      sampledMoves.Subscribe(move => Move(i, move));
      return sampledMoves;
    }).ToArray();
  }

  public async void Start()
  {
    Status = GameStatus.Standby;
    await Task.Delay(StandbyDuration);
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
      CalculateScore();
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

  private void CalculateScore()
  {
    var score1 = 0;
    var score2 = 0;
    foreach (var o in Occupations)
    {
      if (o == 1) { score1++; }
      else if (o == -1) { score2++; }
    }
    Team1Score = score1;
    Team2Score = score2;
  }

  private void CheckVictory()
  {
    // TODO: implement 胜利条件
  }

}
