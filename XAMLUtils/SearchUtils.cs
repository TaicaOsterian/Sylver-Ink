using SylverInk.Notes;
using SylverInk.XAML;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Documents;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAMLUtils;

public static class SearchUtils
{
	public static async Task PerformSearch(this Search window)
	{
		foreach (Database db in Databases)
			await window.SearchDatabase(db);
	}

	public static async Task SearchDatabase(this Search window, Database db)
	{
		CommonUtils.Settings.SearchResults.Clear();
		db.UpdateWordPercentages();

		List<NoteRecord> results = [];

		ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(CommonUtils.Settings.SearchResults);
		view.CustomSort ??= Comparer<NoteRecord>.Create(new((r1, r2) => r2.MatchTags(window.Query).CompareTo(r1.MatchTags(window.Query))));

		for (int i = 0; i < db.RecordCount; i++)
		{
			if (db.GetRecord(i) is not NoteRecord newRecord)
				continue;

			bool textFound = await SearchRecord(window, newRecord);

			if (!textFound)
				continue;

			results.Add(newRecord);
		}

		for (int i = 0; i < results.Count; i++)
			CommonUtils.Settings.SearchResults.Add(results[i]);

		window.DoQuery.Content = "Query";
		window.DoQuery.IsEnabled = true;
	}

	private static async Task<bool> SearchRecord(this Search window, NoteRecord record) => await Task.Run(() =>
	{
		var document = Concurrent(record.GetDocument);
		TextPointer? pointer = document.ContentStart;
		while (pointer is not null && pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.None)
		{
			while (pointer is not null && pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
				pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);

			if (pointer is null)
				break;

			string recordText = pointer.GetTextInRun(LogicalDirection.Forward);
			if (recordText.Contains(window.Query, StringComparison.OrdinalIgnoreCase))
				return true;

			while (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
				pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
		}

		return false;
	});
}
