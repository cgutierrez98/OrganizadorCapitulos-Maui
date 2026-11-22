using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OrganizadorCapitulos.Maui.Services.Interfaces;

#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
#endif

namespace OrganizadorCapitulos.Maui;

public partial class MainPage : ContentPage
{
	private readonly IDragDropService? _dragDropService;

	public MainPage()
	{
		InitializeComponent();
	}

	public MainPage(IDragDropService dragDropService) : this()
	{
		_dragDropService = dragDropService;
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();
#if WINDOWS
		if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
		{
			element.AllowDrop = true;
			element.DragOver -= OnWindowsDragOver;
			element.DragOver += OnWindowsDragOver;
			element.Drop -= OnWindowsDrop;
			element.Drop += OnWindowsDrop;
		}
#endif
	}

#if WINDOWS
	private IDragDropService? ResolveDragDropService() =>
		_dragDropService ?? Handler?.MauiContext?.Services?.GetService<IDragDropService>();

	private void OnWindowsDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
	{
		if (e.DataView.Contains(StandardDataFormats.StorageItems))
		{
			e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
		}
	}

	private async void OnWindowsDrop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
	{
		try
		{
			if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
			var items = await e.DataView.GetStorageItemsAsync();
			if (items == null || items.Count == 0) return;

			var service = ResolveDragDropService();
			if (service == null) return;

			var jsonFile = items
				.OfType<StorageFile>()
				.FirstOrDefault(f => string.Equals(Path.GetExtension(f.Path), ".json", StringComparison.OrdinalIgnoreCase));

			if (jsonFile != null)
			{
				service.NotifySessionFileDropped(jsonFile.Path);
				return;
			}

			var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items)
			{
				if (item is StorageFolder folder && !string.IsNullOrWhiteSpace(folder.Path))
				{
					folders.Add(folder.Path);
				}
				else if (item is StorageFile file && !string.IsNullOrWhiteSpace(file.Path))
				{
					var parent = Path.GetDirectoryName(file.Path);
					if (!string.IsNullOrWhiteSpace(parent))
					{
						folders.Add(parent);
					}
				}
			}

			if (folders.Count > 0)
			{
				service.NotifyFoldersDropped(folders.ToList());
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[MainPage] Drop error: {ex.Message}");
		}
	}
#endif
}
