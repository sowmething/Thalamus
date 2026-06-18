using System.Windows;

namespace Thalamus
{
    public partial class ArgEdit : Window
    {
        public string CompilerArgs { get; private set; }

        public ArgEdit(string currentArgs)
        {
            InitializeComponent();
            MessageBox.Show("Warning!\nDo not change output file (payload.dll) and input file (temp.cpp) or compiling will fail.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            ArgsBox.Text = currentArgs;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            CompilerArgs = ArgsBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
