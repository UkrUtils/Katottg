using System.Text;
using UkrUtils.Katottg.Tool;

Console.OutputEncoding = Encoding.UTF8;
return KatottgCli.Run(args, Console.Out, Console.Error);
