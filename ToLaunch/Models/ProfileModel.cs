namespace ToLaunch.Models;

using System.Collections.Generic;

public class ProfileModel
{
    public string? MainProgramPath { get; set; }
    public string? MainProgramIconPath { get; set; }
    public List<ProgramModel> Programs { get; set; } = new();
}
