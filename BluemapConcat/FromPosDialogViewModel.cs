using CommunityToolkit.Mvvm.ComponentModel;

namespace BluemapConcat
{
    public partial class FromPosDialogViewModel: ObservableObject
    {
        [ObservableProperty] int x1, x2;
        [ObservableProperty] int pos1, pos2;
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
