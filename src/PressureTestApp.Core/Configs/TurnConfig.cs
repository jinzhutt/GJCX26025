namespace PressureTestApp.Core.Configs;

public class TurnConfig
{
    public bool Enabled { get; set; } = true;
    
    public string PortName { get; set; } = "COM3";
    
    public int BaudRate { get; set; } = 9600;
}