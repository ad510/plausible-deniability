// application entry point
// Copyright (c) 2013 Andrew Downing
// This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/.
// If this license is too restrictive for you, the copy on GitHub at https://github.com/ad510/decoherence is licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Decoherence
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new App());
        }
    }
}
