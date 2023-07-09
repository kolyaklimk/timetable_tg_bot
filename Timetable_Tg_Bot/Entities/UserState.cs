﻿namespace TimetableTgBot.Entities;

public class UserState
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public bool WaitingForText { get; set; } = false;
}
