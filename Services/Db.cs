using System;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Zhuanzhu.WebApi.Services
{
    public class Db
    {
        private readonly string _connectionString;

        public Db(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Zhuanzhu");
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("配置项 ConnectionStrings:Zhuanzhu 未设置。");
            }
        }

        public string ConnectionString
        {
            get { return _connectionString; }
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
