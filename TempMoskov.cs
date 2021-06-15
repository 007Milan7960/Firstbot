using System;
using System.Collections.Generic;
using System.Text;

namespace Телеграм_бот
{
    class Temperatura
    {
        public main main;
    }
    class main
    {
        private double _temp;
        public double temp
        {
            get
            {
                return _temp;
            }
            set
            {
                _temp = value - 273.15;
                _temp = Math.Round(_temp, 2);
            }
        }
    }
}
