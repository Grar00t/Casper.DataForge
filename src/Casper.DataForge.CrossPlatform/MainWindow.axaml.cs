using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
namespace Casper.DataForge.CrossPlatform;
public partial class MainWindow : Window
{
 private OutputFormat _format = OutputFormat.Json;
 public MainWindow(){InitializeComponent();Render();}
 private void InputTextBox_TextChanged(object? s, TextChangedEventArgs e){AutoDirection();Render();}
 private void Json_Click(object? s,RoutedEventArgs e){_format=OutputFormat.Json;Render();}
 private void Jsonl_Click(object? s,RoutedEventArgs e){_format=OutputFormat.Jsonl;Render();}
 private void AutoDirection_Click(object? s,RoutedEventArgs e)=>AutoDirection();
 private void RtlDirection_Click(object? s,RoutedEventArgs e)=>InputTextBox.FlowDirection=Avalonia.Media.FlowDirection.RightToLeft;
 private void LtrDirection_Click(object? s,RoutedEventArgs e)=>InputTextBox.FlowDirection=Avalonia.Media.FlowDirection.LeftToRight;
 private async void Copy_Click(object? s,RoutedEventArgs e){var c=TopLevel.GetTopLevel(this)?.Clipboard;if(c is null)return;await c.SetTextAsync(OutputTextBox.Text??string.Empty);StatusText.Text="Copied";}
 private async void Save_Click(object? s,RoutedEventArgs e){var ext=_format==OutputFormat.Json?"json":"jsonl";var f=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{SuggestedFileName="dataforge."+ext,DefaultExtension=ext,FileTypeChoices=new List<FilePickerFileType>{new(_format==OutputFormat.Json?"JSON":"JSON Lines"){Patterns=new[]{"*."+ext}}}});if(f is null)return;await using var stream=await f.OpenWriteAsync();stream.SetLength(0);await using var writer=new StreamWriter(stream,new UTF8Encoding(false));await writer.WriteAsync(OutputTextBox.Text??string.Empty);StatusText.Text="Saved: "+f.Name;}
 private void AutoDirection(){InputTextBox.FlowDirection=DirectionDetector.ContainsArabic(InputTextBox.Text??string.Empty)?Avalonia.Media.FlowDirection.RightToLeft:Avalonia.Media.FlowDirection.LeftToRight;}
 private void Render(){var source=InputTextBox.Text??string.Empty;OutputTextBox.Text=DeterministicConverter.Convert(source,_format);StatusText.Text=$"{_format.ToString().ToUpperInvariant()} | {source.Length} chars";}
}
