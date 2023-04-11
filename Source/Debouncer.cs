﻿using System;
using System.Threading;

public class Debouncer
{
	private Timer _debounceTimer;
	private readonly object _lockObject = new object();
	private readonly int _debounceInterval;

	public Debouncer(int debounceInterval)
	{
		_debounceInterval = debounceInterval;
	}

	public void Debounce(Action action)
	{
		lock (_lockObject)
		{
			if (_debounceTimer != null)
			{
				_debounceTimer.Dispose();
			}

			_debounceTimer = new Timer(
				 _ => action(),
				 null,
				 _debounceInterval,
				 Timeout.Infinite);
		}
	}
}