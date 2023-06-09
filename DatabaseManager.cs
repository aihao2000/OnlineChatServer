using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace OnlineChatServer{
    class DatabaseManager
    {
        SqliteConnection connection;
        public bool Start()
        {
            connection=new SqliteConnection("Data Source=OnlineChat.db");
            connection.Open();
            Console.WriteLine("连接成功");
            CreateuUserInformationTable();
            CreateUserFriendListTable();
            return true;
        }
        private void CreateUserFriendListTable(bool is_async=false)
        {
            using (SqliteCommand command=connection.CreateCommand())
            {
                command.CommandText=
                @"CREATE TABLE IF NOT EXISTS UserFriendList(
                id TEXT,
                friend_id TEXT,
                PRIMARY KEY(id,friend_id),
                FOREIGN KEY(id) REFERENCES UserInfo(id),
                FOREIGN KEY(friend_id) REFERENCES UserInfo(id)
                )";
                if(is_async)
                {
                    command.ExecuteNonQueryAsync();
                }
                else
                {
                    command.ExecuteNonQuery();
                }
            }
            
        }
        public void CreateuUserInformationTable(bool is_async=false)
        {
            using (SqliteCommand command=connection.CreateCommand())
            {
                command.CommandText=
                @"CREATE TABLE IF NOT EXISTS UserInfo(
                id TEXT PRIMARY KEY,
                password TEXT,
                name TEXT,
                email TEXT,
                phone_number TEXT
                )";
                if(is_async)
                {
                    command.ExecuteNonQueryAsync();
                }
                else
                {
                    command.ExecuteNonQuery();
                }
            }
            
        }
        public string AddNewUser(string id,string password,string name="",string email="",string phone_number="",bool is_async=false)
        {
            using (SqliteCommand command=connection.CreateCommand())
            {
                command.CommandText=
                @"INSERT INTO UserInfo(id,password,name,email,phone_number)
                VALUES(@id,@password,@name,,@email,@phone_number)";
                command.Parameters.AddWithValue("@id",id);
                command.Parameters.AddWithValue("@password", password);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@email", email);
                command.Parameters.AddWithValue("@phone_number", phone_number);
                if(is_async)
                {
                    command.ExecuteNonQueryAsync();
                }
                else
                {
                    command.ExecuteNonQuery();
                }
                
            }
            return "true";
        }
        public string GetFriendList(string id)
        {
            using (SqliteCommand command=connection.CreateCommand())
            {
                command.CommandText=
                @"SELECT friend_id
                  FROM  UserFriendList
                  WHERE @id=id";
                command.Parameters.AddWithValue("@id",id);
                using(SqliteDataReader reader=command.ExecuteReader())
                {
                    List<string> friend_list=new List<string>();
                    while(reader.Read())
                    {
                        string name=reader.GetString(0);
                        friend_list.Add(name);
                    }
                    return JsonSerializer.Serialize(friend_list);
                }
            }
        }
        ~DatabaseManager()
        {
            connection.Close();
        }
    }
}