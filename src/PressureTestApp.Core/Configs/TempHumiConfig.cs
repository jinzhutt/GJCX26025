namespace PressureTestApp.Core.Configs;

public class TempHumiConfig
{
    public bool Enabled { get; set; } = true;
    
    public string PortName { get; set; } = "/dev/ttyUSB0";
    
    public int BaudRate { get; set; } = 9600;
}
