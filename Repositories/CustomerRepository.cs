using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Utils;
using Dapper;

namespace DChemist.Repositories
{
    public class CustomerRepository
    {
        private readonly DatabaseService _db;

        public CustomerRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<Customer?> FindOrCreateAsync(string name, string? phone)
        {
            try
            {
                const string findQuery = @"
                    SELECT 
                        id, 
                        customer_name as CustomerName, 
                        phone 
                    FROM customers 
                    WHERE customer_name = @name AND phone = @phone 
                    LIMIT 1";

                using var conn = _db.GetConnection();
                var customer = await conn.QuerySingleOrDefaultAsync<Customer>(findQuery, new { name, phone });
                
                if (customer != null) return customer;

                const string insertQuery = "INSERT INTO customers (customer_name, phone) VALUES (@name, @phone) RETURNING id;";
                int id = await conn.ExecuteScalarAsync<int>(insertQuery, new { name, phone });
                
                AppLogger.LogInfo($"Customer created: id={id}, name={name}");
                return new Customer { Id = id, CustomerName = name, Phone = phone };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CustomerRepository.FindOrCreateAsync failed", ex);
                throw new DataAccessException("Could not save customer information.", ex);
            }
        }
    }
}
