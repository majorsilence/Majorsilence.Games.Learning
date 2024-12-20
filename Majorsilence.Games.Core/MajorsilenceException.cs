﻿using System;

namespace Majorsilence.Games.Core;

public class MajorsilenceException : Exception
{
    public MajorsilenceException(string message) : base(message)
    {
    }

    public MajorsilenceException(string message, Exception ex) : base(message, ex)
    {
    }
}