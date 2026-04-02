using System;

namespace API.DB;

public partial class Userplaybacksetting
{
    public int UserId { get; set; }

    public bool ShuffleEnabled { get; set; }

    public string RepeatMode { get; set; } = "Off";

    public bool AutoplayEnabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
