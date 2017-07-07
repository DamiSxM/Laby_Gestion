using Laby_Interfaces;
using System.Drawing;

namespace Laby_Gestion
{
    public class Gestion
    {
        int[,] _labyrinthe;
        IAffichage _affichage;
        ILiaison _liaison;

        public Gestion(int[,] labyrinthe, IAffichage affichage, ILiaison liaison)
        {
            _labyrinthe = labyrinthe;
            _affichage = affichage;
            _liaison = liaison;

            _affichage.PositionChanged += PositionChanged;

            _liaison.DataReceived += DataReceived;
            _liaison.FinRechercheServer += FinRechercheServer;
        }

        private void PositionChanged(int x, int y)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("X {0}, Y {1}", x, y));
            _liaison.SendData(new Point(x, y));
        }

        private void FinRechercheServer(bool isserver)
        {
            if (isserver)
            {

            }
            _affichage.Warfog(4);
            _affichage.PlayerAdd("bob", 5, 5);
            _affichage.PersoTeleport(3, 3);
        }

        private void DataReceived(string sender, object data)
        {
            System.Diagnostics.Debug.WriteLine((Point)data);
        }
    }
}
