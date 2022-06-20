using System;
using System.Collections.Generic;
using System.Text;
using VersionInfoMVVM.Models;

namespace VersionInfoMVVM.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel(DataUnit dataUnit)
        {
            Data = new VersionInfoViewModel(dataUnit);
        }
        public VersionInfoViewModel Data { get; }  
    }
}
