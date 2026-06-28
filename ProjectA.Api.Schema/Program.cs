using ProjectA.Api.Schema;

var connectionString =
    args.FirstOrDefault();

if (string.IsNullOrEmpty(connectionString))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("No connection string");
    Console.ResetColor();
    return;
}

Console.WriteLine("Upgrading database...");
if (Upgrader.Upgrade(connectionString))
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Success!");
    Console.ResetColor();
}
