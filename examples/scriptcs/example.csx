#r "PlinqEtl.Core.dll"

using PlinqEtl.Core;

var lines =
	from line in File.ReadLines("input.tsv").Skip(1).ToCatching()
	select line.Split('\t') into cells
	let id = int.Parse(cells[0])
	let value = int.Parse(cells[2])
	select new { 
		id, 
		name = cells[1], 
		value,
		calculated = id + value * 2 };

foreach (var line in lines)
	Console.WriteLine(line);

Console.WriteLine();
Console.WriteLine("Catched errors:");
foreach (var exception in lines.GetExceptions())
	Console.WriteLine(exception.Message);
