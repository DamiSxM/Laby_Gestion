using Laby_Interfaces;
using Laby_Maze;
using System.Drawing;

namespace Laby_Gestion
{
    public class Gestion
    {
        Maze _labyrinthe;
        IAffichage _affichage;
        ILiaison _liaison;

        public Gestion(Maze labyrinthe, IAffichage affichage, ILiaison liaison)
        {
            _labyrinthe = labyrinthe;
            _affichage = affichage;
            _liaison = liaison;

            PersoRandomPosition();
            _affichage.PositionChanged += PositionChanged;

            _liaison.DataReceived += DataReceived;
            _liaison.ClientConnected += ClientConnected;
            _liaison.FinRechercheServer += FinRechercheServer;
        }

        void PersoRandomPosition()
        {
            System.Random rnd = new System.Random();
            int x, y;
            do
            {
                x = rnd.Next(0, _labyrinthe.Taille - 1);
                y = rnd.Next(0, _labyrinthe.Taille - 1);
            } while (_labyrinthe.Labyrinthe[x, y] != 0);

            System.Diagnostics.Debug.WriteLine(string.Format("PersoRandomPosition : X {0}, Y {1}", x, y));
            _affichage.PersoTeleport(x, y);
        }

        private void ClientConnected(string ip)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Le client {0} vient de se connecter", ip));
            _liaison.SendDataTo(_labyrinthe.Labyrinthe, ip); // Envoyer labyrinthe
            int[] pos = _affichage.PersoGetPosition();
            _liaison.SendDataTo(PersoGetPosition(), ip);
        }

        Point PersoGetPosition()
        {
            int[] pos = _affichage.PersoGetPosition();
            return new Point(pos[0], pos[1]);
        }
        private void PositionChanged(int x, int y)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("PositionChanged : X {0}, Y {1}", x, y));
            if (_liaison.IsServer()) // Si on est le server
            {
                _liaison.SendData(new Point(x, y)); // Si il y a des clients, on leur envoi sa position
            }
            else // Si on est un client
            {
                _liaison.SendData(new Point(x, y)); // On leur envoi sa position au server
            }
        }

        private void FinRechercheServer(bool isserver)
        {
            if (!isserver)
            {
                _affichage.Warfog(4);
            }
        }

        private void DataReceived(string sender, object data)
        {
            if (_liaison.IsServer()) System.Diagnostics.Debug.WriteLine("SERVER");
            else System.Diagnostics.Debug.WriteLine("CLIENT");
            System.Diagnostics.Debug.WriteLine(data.GetType().ToString());

            if (data.GetType() == typeof(Point)) ReceptionPoint(sender, (Point)data);
            if (data.GetType() == typeof(string)) ReceptionString(sender, (string)data);
            if (data.GetType() == typeof(int[,])) ReceptionLabyrinthe((int[,])data);
        }
        void ReceptionLabyrinthe(int[,] lab)
        {
            System.Diagnostics.Debug.WriteLine("Nouveau Labyrinthe !");
            _labyrinthe.Labyrinthe = lab;
            _affichage.LabyUpdate();
            PersoRandomPosition();
            _liaison.SendData(PersoGetPosition());
        }
        void ReceptionString(string ip, string s)
        {
            System.Diagnostics.Debug.WriteLine("Commande de " + ip + " : " + s);
        }
        void ReceptionPoint(string ip, Point p)
        {
            if (_affichage.PlayerExists(ip))
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} existe, move x{1}, y{2}", ip, p.X, p.Y));
                _affichage.PlayerMove(ip, p.X, p.Y);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} existe pas, add x{1}, y{2}", ip, p.X, p.Y));
                System.Diagnostics.Debug.WriteLine(ip + " n'existe pas dans la liste");
                _affichage.PlayerAdd(ip, p.X, p.Y);
            }
            System.Diagnostics.Debug.WriteLine(p);
        }
    }
}
