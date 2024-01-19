using System;

public class Game
{
	public int Size { get; private set; }
	public int[] Status;
	public int[] PlayerPositions;
	public Game(int size, int playerCount)
	{
		Size = size;
		Status = new int[size * size];
		Array.Fill(Status, 0);
		PlayerPositions = new int[playerCount];
		PlayerPositions[0] = Status.Length / 2 - 3; PlayerPositions[1] = Status.Length / 2 + 3;
		for (int i = 0; i < playerCount; i++)
		{
			Move(i, Direction.Still, Direction.Still);
		}
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

	public bool Move(int playerId, Direction x, Direction y)
	{
		var newPosition = (PlayerPositions[playerId] + (int)x + (int)y * Size + Size * Size) % (Size * Size);
		PlayerPositions[playerId] = newPosition;
		Status[newPosition] = playerId % 2 == 0 ? 1 : -1;
		return true;
	}
	public bool Move(int playerId, int code)
	{
		var step = new Step(code);
		return Move(playerId, step.X, step.Y);
	}

}
