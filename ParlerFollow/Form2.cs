﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ParlerFollow
{
    public partial class Form2 : Form
    {

        bool KeyRegistered = false;
        public Form2()
        {
            InitializeComponent();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            string password = textBox1.Text;
            if (password == "55a73918-b3dc-4af3-9d95-cee9110c61d3")
            {
                RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");
                KeyRegistered = true;
                rkey.SetValue("KeyRegistered_voter", KeyRegistered);
                this.Close();
            } else
            {
                MessageBox.Show(this, "Invalid Key, Please try again.",
                                   "Your Key", MessageBoxButtons.OK,
                                   MessageBoxIcon.Error,
                                   MessageBoxDefaultButton.Button1, 0);
            }
        }


    }
}
