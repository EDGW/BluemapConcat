using CommunityToolkit.Mvvm.ComponentModel;

namespace BluemapConcat
{
    public partial class FromPosDialogViewModel(int start1, int start2) : ObservableObject
    {
        [ObservableProperty] int x1 = start1, x2 = start2;
        [ObservableProperty] int pos1 = start1 * 512, pos2 = start2 * 512;
        public int Convert(int pos)
        {
            double p = (double)pos;
            p /= 512;
            return (int)Math.Floor(p);
        }

        partial void OnPos1Changed(int value)
        {
            X1 = Convert(value);
        }

        partial void OnPos2Changed(int value)
        {
            X2 = Convert(value);
        }
    }
}
