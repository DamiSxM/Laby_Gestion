using Laby_Interfaces;
using Laby_Maze;
using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Laby_Gestion
{
    [Serializable]
    public class DataPosition
    {
        object _data;
        Point _position;
        public object Data { get { return _data; } set { _data = value; } }
        public Point Position { get { return _position; } set { _position = value; } }

        public DataPosition() { }
        public DataPosition(object data, Point position)
        {
            _data = data;
            _position = position;
        }
        public DataPosition(SerializationInfo info, StreamingContext context) { }
    }
    [Serializable]
    public class ObjectInstruction
    {
        object _data;
        string _type;
        string _instruction;
        public object Data { get { return _data; } set { _data = value; } }
        public string Type { get { return _type; } set { _type = value; } }
        public string Instruction { get { return _instruction; } set { _instruction = value; } }

        public ObjectInstruction() { }
        public ObjectInstruction(object data, string type, string instruction)
        {
            _data = data;
            _type = type;
            _instruction = instruction;
        }
        public ObjectInstruction(SerializationInfo info, StreamingContext context) { }
    }

    public class Gestion
    {
        Maze _labyrinthe;
        IAffichage _affichage;
        ILiaison _liaison;

        Hashtable _items = new Hashtable();

        public Gestion(Maze labyrinthe, IAffichage affichage, ILiaison liaison)
        {
            _labyrinthe = labyrinthe;
            _affichage = affichage;
            _liaison = liaison;

            _affichage.PositionChanged += PositionChanged;

            _liaison.DataReceived += DataReceived;
            _liaison.ClientConnected += ClientConnected;
            _liaison.FinRechercheServer += FinRechercheServer;

            PersoRandomPosition();

            if (_liaison.IsServer()) // Gestion est créer après Liaison, donc l'évent FinRechercheServer est passé avant le += ligne 64
            {
                FinRechercheServer(true);
            }
        }

        void PersoRandomPosition()
        {
            System.Random rnd = new System.Random();
            int x, y;
            do
            {
                x = rnd.Next(0, _labyrinthe.Taille - 2);
                y = rnd.Next(0, _labyrinthe.Taille - 2);
            } while (_labyrinthe.Labyrinthe[x, y] != 0);

            System.Diagnostics.Debug.WriteLine(string.Format("PersoRandomPosition : X {0}, Y {1}", x, y));
            PersoTeleport(new Point(x, y));
        }

        void GenerationItems()
        {
            System.Diagnostics.Debug.WriteLine(string.Format("GenerationItems"));
            System.Random rnd = new System.Random();
            for (int i = 0; i < _labyrinthe.Taille; i++)
            {
                Point p;
                int x, y;
                bool isWall, isTileNotFree;
                do
                {
                    x = rnd.Next(0, _labyrinthe.Taille - 2);
                    y = rnd.Next(0, _labyrinthe.Taille - 2);
                    p = new Point(x, y);
                    isWall = _labyrinthe.Labyrinthe[x, y] == 1;
                    isTileNotFree = _items.ContainsKey(p);
                } while (isWall & isTileNotFree); // Si c'est du sol et qu'il n'y a rien

                try
                {
                    _items.Add(p, "GrilleFermee");
                }
                catch (Exception) { }
            }
            _affichage.ItemsInit(_items);
            System.Diagnostics.Debug.WriteLine(string.Format("GenerationItems : {0} items", _items.Count));
        }

        void ClientConnected(string ip)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Le client {0} vient de se connecter", ip));
            _liaison.SendDataTo(_labyrinthe.Labyrinthe, ip); // Envoyer labyrinthe

            _liaison.SendDataTo(new DataPosition("player", _affichage.PersoGetPosition()), ip); // Envoi position server

            _liaison.SendDataTo(_items, ip);
        }

        private void PositionChanged(int x, int y)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("PositionChanged : X {0}, Y {1}", x, y));
            System.Diagnostics.Debug.WriteLine(string.Format("PositionChanged : {0}", _labyrinthe.Labyrinthe[x, y]));
            if (_liaison.IsServer()) // Si on est le server
            {
                ObjectInstruction data = new ObjectInstruction(new Point(x, y), "player", "move");
                _liaison.SendData(data);
                //DataPosition dp = new DataPosition("player", new Point(x, y));
                //_liaison.SendData(dp); // Si il y a des clients, on leur envoi sa position
            }
            else // Si on est un client
            {
                ObjectInstruction data = new ObjectInstruction(new Point(x, y), "player", "move");
                _liaison.SendData(data);
                //DataPosition dp = new DataPosition("player", new Point(x, y));
                //_liaison.SendData(dp); // On leur envoi sa position au server
            }
        }

        void FinRechercheServer(bool isserver)
        {
            System.Diagnostics.Debug.WriteLine("Gestion:FinRechercheServer isserver " + isserver);
            if (isserver)
            {
                _affichage.Debug("SERVER");
                GenerationItems();
                _affichage.Warfog(2);
            }
            else
            {
                _affichage.Debug("CLIENT");
                _affichage.Warfog(4);
            }
        }

        void DataReceived(string sender, object data)
        {
            if (_liaison.IsServer())
            {
                System.Diagnostics.Debug.WriteLine("SERVER");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CLIENT");
            }
            System.Diagnostics.Debug.WriteLine(data.GetType().ToString());

            if (data.GetType() == typeof(string)) ReceptionString(sender, (string)data);
            if (data.GetType() == typeof(int[,])) ReceptionLabyrinthe((int[,])data);
            if (data.GetType() == typeof(Hashtable)) ReceptionItems(sender, (Hashtable)data);
            if (data.GetType() == typeof(ObjectInstruction)) ReceptionObjectInstruction(sender, (ObjectInstruction)data);
        }

        private void ReceptionItems(string sender, Hashtable items)
        {
            _items = items;
            _affichage.ItemsInit(_items);
            System.Diagnostics.Debug.WriteLine(string.Format("Hashtable items {0}", _items.Count));

            /*foreach (DictionaryEntry e in _items)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("DictionaryEntry Key : {1}, Value : {2}", e.Key, e.Value));
                //ReceptionItem((Point)e.Key);
            }*/

        }

        private void ReceptionObjectInstruction(string sender, ObjectInstruction data)
        {
            if (data.Type == "item")
            {
                if (data.Instruction == "add")
                {
                    DataPosition dp = (DataPosition)data.Data;
                    System.Diagnostics.Debug.WriteLine(string.Format("ReceptionObjectInstruction : Item add : X {0}, Y {1}", dp.Position.X, dp.Position.Y));
                    _affichage.ItemAdd(dp.Position, (string)dp.Data);
                }
                else if (data.Instruction == "remove")
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("ReceptionObjectInstruction : Item remove"));
                    _affichage.ItemRemove((Point)data.Data);
                }
            }
            else if (data.Type == "player")
            {
                if (data.Instruction == "move")
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("ReceptionObjectInstruction : Player move"));
                    ReceptionPlayer(sender, (Point)data.Data);
                }
            }
        }

        void ReceptionLabyrinthe(int[,] lab)
        {
            System.Diagnostics.Debug.WriteLine("Nouveau Labyrinthe !");
            _labyrinthe.Labyrinthe = lab;
            _affichage.LabyUpdate();
            PersoRandomPosition();
            _liaison.SendData(new DataPosition("player", _affichage.PersoGetPosition()));
        }
        void ReceptionString(string ip, string s)
        {
            System.Diagnostics.Debug.WriteLine("Commande de " + ip + " : " + s);
        }
        void ReceptionPlayer(string ip, Point p)
        {
            if (_affichage.PlayerExists(ip))
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} existe, move x{1}, y{2}", ip, p.X, p.Y));
                _affichage.PlayerMove(ip, p);

                if (_items.Contains(p))
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("ITEM !"));
                    ItemRemove(p);
                }

                if (p == _affichage.PersoGetPosition())
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("WAAAAAAAAAAAAAAAAAAAAG !"));
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} existe pas, add x{1}, y{2}", ip, p.X, p.Y));
                _affichage.PlayerAdd(ip, p);
            }
            System.Diagnostics.Debug.WriteLine(string.Format("PlayerMove Perso[{0},{1}] Player[{2},{3}]", _affichage.PersoGetPosition().X, _affichage.PersoGetPosition().Y, p.X, p.Y));
        }
        void ReceptionItem(Point p)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("Ajout point x{1}, y{2}", p.X, p.Y));
            //_affichage.ItemAdd(p.X, p.Y, "GrilleFermee");
        }

        public void PersoMove(Direction d)
        {
            switch (d)
            {
                case Direction.LEFT:
                    _affichage.PersoMoveLeft();
                    break;
                case Direction.UP:
                    _affichage.PersoMoveUp();
                    break;
                case Direction.RIGHT:
                    _affichage.PersoMoveRight();
                    break;
                case Direction.DOWN:
                    _affichage.PersoMoveDown();
                    break;
            }
            //_liaison.SendData(new DataPosition("player", _affichage.PersoGetPosition()));
        }
        public void PersoTeleport(Point p)
        {
            _affichage.PersoTeleport(p);

            if (!_liaison.IsServer())
            {
                _liaison.SendData(new DataPosition("player", _affichage.PersoGetPosition()));
            }
            else if (_liaison.GetClientsCount() > 0)
            {
                _liaison.SendData(new DataPosition("player", _affichage.PersoGetPosition()));
            }
        }

        public void PlayerAdd(string ip, Point p)
        {
            _affichage.PlayerAdd(ip, p);
        }
        public void PlayerMove(string ip, Point p)
        {
            _affichage.PlayerMove(ip, p);
        }
        public void PlayerRemove(string ip)
        {
            _affichage.PlayerRemove(ip);
        }

        public void ItemAdd(Point p, string s)
        {
            _affichage.ItemAdd(p, s);
            if (_liaison.IsFinRechercheServer())
            {
                ObjectInstruction data = new ObjectInstruction(new DataPosition(s, p), "item", "add");
                _liaison.SendData(data);
                System.Diagnostics.Debug.WriteLine(string.Format("ItemAdd {0}, {1} : Envoi", p, s));
            }
        }
        public void ItemRemove(Point p)
        {
            _affichage.ItemRemove(p);
            if (_liaison.IsFinRechercheServer())
            {
                ObjectInstruction data = new ObjectInstruction(new DataPosition(null, p), "item", "remove");
                _liaison.SendData(data);
                System.Diagnostics.Debug.WriteLine(string.Format("ItemRemove {0} : Envoi", p));
            }
        }
    }
}