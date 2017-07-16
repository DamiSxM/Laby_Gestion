using System;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;

namespace Labyrinthe
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

        int _cellSize;
        int _persoVitesse = 2;
        Point _positionCase;
        Point _positionPixel;

        Hashtable _items = new Hashtable();

        public int PersoVitesse { get { return _persoVitesse; } set { _persoVitesse = value; } }

        public Gestion(Maze labyrinthe, IAffichage affichage, ILiaison liaison)
        {
            _labyrinthe = labyrinthe;
            _affichage = affichage;
            _liaison = liaison;

            _affichage.PositionChanged += PositionChanged;
            _cellSize = _affichage.GetCellSize();

            _liaison.DataReceived += DataReceived;
            _liaison.ClientConnected += ClientConnected;
            _liaison.FinRechercheServer += FinRechercheServer;
        }

        string IsServer()
        {
            return _liaison.IsServer() ? "SERVER" : "CLIENT";
        }

        public void Start()
        {
            _liaison.Start();
            PersoRandomPosition();
        }
        public void Start(Etat init)
        {
            _liaison.Start(init);
            PersoRandomPosition();
        }

        void PersoRandomPosition()
        {
            System.Random rnd = new System.Random();
            int x, y;
            do
            {
                x = rnd.Next(_labyrinthe.Taille - 1);
                y = rnd.Next(_labyrinthe.Taille - 1);
            } while (_labyrinthe.Labyrinthe[x, y] != 0);

            System.Diagnostics.Debug.WriteLine(string.Format("{0} : PersoRandomPosition : X {1}, Y {2}", IsServer(), x, y));
            _positionCase = new Point(x, y);
            _positionPixel = new Point(x * _cellSize, y * _cellSize);

            PersoTeleport(_positionCase);
        }

        void GenerationItems()
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.GenerationItems", IsServer()));
            System.Random rnd = new System.Random();
            for (int i = 0; i < _labyrinthe.Taille * 2; i++)
            {
                Point p;
                int x, y;
                bool isWall, isTileNotFree;
                do
                {
                    x = rnd.Next(_labyrinthe.Taille - 1);
                    y = rnd.Next(_labyrinthe.Taille - 1);
                    isWall = _labyrinthe.Labyrinthe[x, y] == 1;
                    isTileNotFree = _items.Contains(new Point(x, y));
                } while (isWall || isTileNotFree); // Si c'est du sol et qu'il n'y a rien

                p = new Point(x, y);
                if (rnd.Next(2) == 0) _items.Add(p, Loot.CRATE);
                else _items.Add(p, Loot.COIN);
            }
            //_items.Clear(); // TEST
            /*_items.Add(new Point(2, 2), Loot.COIN);
            _items.Add(new Point(3, 3), Loot.COIN);*/
            _affichage.ItemsInit(_items);
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.GenerationItems : {1} items", IsServer(), _items.Count));
        }

        void ClientConnected(string ip)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ClientConnected : {1} vient de se connecter", IsServer(), ip));

            _liaison.SendDataTo(_labyrinthe.Labyrinthe, ip); // Envoyer labyrinthe au client
            _liaison.SendDataTo(new DataPosition("player", _affichage.PersoGetPosition()), ip); // Envoi position server au client
            _liaison.SendDataTo(_items, ip); // Envoyer les items au client
        }

        private void PositionChanged(int x, int y)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.PositionChanged : X {1}, Y {2}", IsServer(), x, y));
            Point p = new Point(x, y);

            if (_liaison.IsServer()) // Si on est le server
            {
                ObjectInstruction data = new ObjectInstruction(p, "player", "move");
                _liaison.SendData(data); // Si il y a des clients, on leur envoi sa position
                if (_items.Contains(p))
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("{0} : ITEM ! : {1}", IsServer(), _items[p]));
                    ItemEffect((Loot)_items[p]);
                    ItemRemove(p);
                }
            }
            else // Si on est un client
            {
                ObjectInstruction data = new ObjectInstruction(p, "player", "move");
                _liaison.SendData(data); // On leur envoi sa position au server
            }
        }

        void FinRechercheServer(bool isserver)
        {
            if (isserver)   // Server
            {
                System.Diagnostics.Debug.WriteLine("Gestion.FinRechercheServer : SERVER");
                _affichage.Debug("SERVER");
                GenerationItems();
            }
            else            // Client
            {
                System.Diagnostics.Debug.WriteLine("Gestion.FinRechercheServer : CLIENT");
                _affichage.Debug("CLIENT");
                _liaison.SendData(new ObjectInstruction(_affichage.PersoGetPosition(), "player", "move"));
            }
        }

        void DataReceived(string sender, object data)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.DataReceived : {1}", IsServer(), data.GetType().ToString()));

            if (data.GetType() == typeof(string)) ReceptionString(sender, (string)data);
            if (data.GetType() == typeof(int[,])) ReceptionLabyrinthe((int[,])data);
            if (data.GetType() == typeof(Hashtable)) ReceptionItems(sender, (Hashtable)data);
            if (data.GetType() == typeof(ObjectInstruction)) ReceptionObjectInstruction(sender, (ObjectInstruction)data);
        }

        private void ReceptionItems(string sender, Hashtable items)
        {
            _items = items;
            _affichage.ItemsInit(_items);
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionItems : {1} items", IsServer(), _items.Count));
        }

        private void ReceptionObjectInstruction(string sender, ObjectInstruction data)
        {
            if (data.Type == "item")
            {
                if (data.Instruction == "add")
                {
                    DataPosition dp = (DataPosition)data.Data;
                    System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionObjectInstruction : Item add : X {1}, Y {2}", IsServer(), dp.Position.X, dp.Position.Y));
                    _affichage.ItemAdd(dp.Position, (Loot)dp.Data);
                }
                else if (data.Instruction == "remove")
                {
                    Point p = (Point)data.Data;
                    System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionObjectInstruction : Item remove : X {1}, Y {2}", IsServer(), p.X, p.Y));
                    ItemRemove(p);
                }
            }
            else if (data.Type == "player")
            {
                if (data.Instruction == "move")
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionObjectInstruction : Player move", IsServer()));
                    ReceptionPlayer(sender, (Point)data.Data);
                }
            }
            else if (data.Type == "effect")
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionObjectInstruction : effect {1}", IsServer(), data.Instruction));
                if (data.Instruction == "addVitesse")
                {
                    PersoVitesse++;
                }
                else if (data.Instruction == "addVision")
                {
                    _affichage.WarfogSet(_affichage.WarfogGet() + 1);
                }
            }
        }

        void ReceptionLabyrinthe(int[,] lab)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionLabyrinthe : Nouveau Labyrinthe ! x{1} y{1}", IsServer(), lab.GetLength(0)));
            _labyrinthe.Labyrinthe = lab;
            _affichage.LabyUpdate();
            PersoRandomPosition();
            _liaison.SendData(new DataPosition("player", _affichage.PersoGetPosition()));
        }
        void ReceptionString(string ip, string s)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Commande de {1} : {2}", IsServer(), ip, s));
        }
        void ReceptionPlayer(string ip, Point p)
        {

            if (_affichage.PlayerExists(ip)) // Si le player existe déjà : move
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionPlayer : Perso[{1},{2}], PlayerMove[{3},{4}] -> {5}", IsServer(), _affichage.PersoGetPosition().X, _affichage.PersoGetPosition().Y, p.X, p.Y, ip));
                _affichage.PlayerMove(ip, p);

            }
            else // Si le player n'existe pas déjà : add
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionPlayer : Perso[{1},{2}] PlayerAdd[{3},{4}] -> {5}", IsServer(), _affichage.PersoGetPosition().X, _affichage.PersoGetPosition().Y, p.X, p.Y, ip));
                _affichage.PlayerAdd(ip, p);
            }

            if (_items.Contains(p)) // Si le player se déplace sur un objet :
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ReceptionPlayer : ITEM ! : {1}", IsServer(), _items[p]));
                switch ((Loot)_items[p])
                {
                    case Loot.CRATE:
                        _liaison.SendDataTo(new ObjectInstruction(null, "effect", "addVision"), ip);
                        break;
                    case Loot.COIN:
                        _liaison.SendDataTo(new ObjectInstruction(null, "effect", "addVitesse"), ip);
                        break;
                }
                ItemRemove(p);
            }
            if (p == _affichage.PersoGetPosition()) // Si le player se déplace sur le joueur :
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : FIGHT !", IsServer()));
            }
        }

        public void PersoMove(Direction d)
        {
            // GetPersoPosition
            Point currentPositionPixel = _affichage.PersoGetPositionPixel();

            int x = 0, y = 0;
            switch (d)
            {
                case Direction.LEFT:
                    x -= _persoVitesse;
                    break;
                case Direction.UP:
                    y -= _persoVitesse;
                    break;
                case Direction.RIGHT:
                    x += _persoVitesse;
                    break;
                case Direction.DOWN:
                    y += _persoVitesse;
                    break;
            }

            Point newPosition = new Point(currentPositionPixel.X + x, currentPositionPixel.Y + y);
            Point tmp = new Point((currentPositionPixel.X + x) / _cellSize, (currentPositionPixel.Y + y) / _cellSize);
            Point tmpDown = new Point((currentPositionPixel.X + x) / _cellSize, (currentPositionPixel.Y + y + _cellSize / 2) / _cellSize);

            int labyCase = _labyrinthe.Labyrinthe[tmp.X, tmp.Y];
            int labyCaseDown = _labyrinthe.Labyrinthe[tmp.X, tmpDown.Y];
            switch (d)
            {
                case Direction.LEFT:
                    if (labyCase != 0)
                        newPosition.X = (tmp.X + 1) * _cellSize;
                    break;

                case Direction.UP:
                    if (labyCase != 0)
                        newPosition.Y = (tmp.Y + 1) * _cellSize;
                    break;

                case Direction.RIGHT:
                    if (labyCase != 0)
                        newPosition.X = tmp.X * _cellSize;
                    break;

                case Direction.DOWN:
                    if (labyCaseDown != 0)
                        newPosition.Y = tmpDown.Y * _cellSize - _cellSize / 2;
                    break;
            }

            _affichage.PersoMove(d, newPosition);
        }
        public void PersoTeleport(Point p)
        {
            _affichage.PersoTeleport(p);

            if (!_liaison.IsServer())
            {
                _liaison.SendData(new ObjectInstruction(p, "player", "move"));
            }
            else if (_liaison.GetClientsCount() > 0)
            {
                _liaison.SendData(new ObjectInstruction(p, "player", "move"));
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

        public void ItemAdd(Point p, Loot s)
        {
            _affichage.ItemAdd(p, s);
            if (_liaison.IsFinRechercheServer())
            {
                ObjectInstruction data = new ObjectInstruction(new DataPosition(s, p), "item", "add");
                _liaison.SendData(data);
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ItemAdd {1}, {2} : Envoi", IsServer(), p, s));
            }
        }
        public void ItemRemove(Point p)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ItemRemove {1}", IsServer(), p));
            _items.Remove(p);
            _affichage.ItemRemove(p);

            if (_liaison.IsServer())
            {
                ObjectInstruction data = new ObjectInstruction(p, "item", "remove");
                _liaison.SendData(data);
                System.Diagnostics.Debug.WriteLine(string.Format("{0} : Gestion.ItemRemove {1} : Envoi aux clients", IsServer(), p));
            }
        }
        void ItemEffect(Loot i)
        {
            switch (i)
            {
                case Loot.CRATE:
                    _affichage.WarfogSet(_affichage.WarfogGet() + 1);
                    break;
                case Loot.COIN:
                    PersoVitesse++;
                    break;
            }
        }
    }
}