using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using WpfApplication1.Annotations;

namespace WpfApplication1
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            ServicePointManager
                    .ServerCertificateValidationCallback +=
                (sender, cert, chain, sslPolicyErrors) => true;

            _clickCommand = new DelegateCommand(async () => await OnClick());
        }

        public class DelegateCommand : ICommand
        {
            private readonly Action _action;

            public DelegateCommand(Action action)
            {
                _action = action;
            }

            public void Execute(object parameter)
            {
                _action();
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }
            
            public event EventHandler CanExecuteChanged;
        }

        public ICommand ClickCommand => _clickCommand;
        private readonly DelegateCommand _clickCommand;
        private bool _isBusy;
        private ImageSource _imageSource;
        private string _imageCaption = "Click on the button to load a random image";

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (_isBusy == value) return;
                
                _isBusy = value;
                OnPropertyChanged();

                Mouse.OverrideCursor = _isBusy ? Cursors.Wait : null;
            }
        }

        public ImageSource ImageSource
        {
            get { return _imageSource; }
            set { _imageSource = value; OnPropertyChanged();}
        }

        public string ImageCaption
        {
            get { return _imageCaption; }
            set { _imageCaption = value; OnPropertyChanged(); }
        }

        private async Task OnClick()
        {
            IsBusy = true;

            string imgAsBase64String;
            Tuple<string, string> tuple;

            do
            {
                tuple = await GetRandomImageUrl();
            }
            while (tuple.Item2.Substring(tuple.Item2.LastIndexOf('.')) == ".svg");

            using (WebClient webClient = new WebClient())
            {
                byte[] bytes = await webClient.DownloadDataTaskAsync(tuple.Item2);
                int imgSizeInKB = bytes.Length / 1000;
                Debug.WriteLine($"File received: {imgSizeInKB:N0}KB");
                imgAsBase64String = Convert.ToBase64String(bytes);
                Debug.WriteLine($"File {tuple.Item1} encoded");
                int encodedStringSizeInKb = Encoding.Unicode.GetByteCount(imgAsBase64String) / 1000;
                Debug.WriteLine($"Encoded string size: {encodedStringSizeInKb:N0}KB ({(double)encodedStringSizeInKb/imgSizeInKB:P})");
            }

            byte[] decodedBytes = Convert.FromBase64String(imgAsBase64String);
            Debug.WriteLine("File decoded");

            using (var stream = new MemoryStream(decodedBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                
                this.ImageSource = bitmap;
            }

            this.ImageCaption = tuple.Item1;
            this.IsBusy = false;
        }

        async Task<Tuple<string, string>> GetRandomImageUrl()
        {
            string apiUrl =
                @"https://commons.wikimedia.org/w/api.php?action=query&generator=random&grnnamespace=6&prop=imageinfo&iiprop=url&format=json";

            using (WebClient webClient = new WebClient())
            {
                var response = webClient.DownloadStringTaskAsync(apiUrl);
                var jObject = JObject.Parse(await response);
                var pagesToken = jObject.SelectToken("query.pages");
                var imgToken = pagesToken.First().First();
                var imgTitle = imgToken["title"].ToString();
                var imgUrl = imgToken["imageinfo"].First()["url"].ToString();
                return new Tuple<string, string>(imgTitle, imgUrl);
            }
        }
    }
}
