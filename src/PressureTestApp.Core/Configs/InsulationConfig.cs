namespace PressureTestApp.Core.Configs;

public class InsulationConfig
{
    public bool Enabled { get; set; } = true;
    
    public string PortName { get; set; } = "COM2";
    
    public int BaudRate { get; set; } = 9600;
}