using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace DbClient.Wpf.Services
{
    /// <summary>
    /// Implementación de la interfaz ICompletionData para proporcionar sugerencias de autocompletado en AvalonEdit.
    /// </summary>
    public class SqlCompletionData : ICompletionData
    {
        public SqlCompletionData(string text, string description = "")
        {
            Text = text;
            Description = description;
        }

        public ImageSource Image => null;

        public string Text { get; }

        // Lo que se muestra en la lista
        public object Content => Text;

        // La descripción emergente al seleccionar el item
        public object Description { get; }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionEventArgs)
        {
            // Reemplaza el texto escrito por la palabra sugerida completa
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
