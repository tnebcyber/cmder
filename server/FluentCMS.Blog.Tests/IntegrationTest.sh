test_sqlite(){
  local db_name=$1
  export DatabaseProvider=Sqlite
  export Sqlite="Data Source=${db_name}"
  sleep 1
  dotnet test 
}

remove_container(){
  local container_name=$1
   # Remove existing container if it exists
   if docker ps -a --format '{{.Names}}' | grep -q "^${container_name}$"; then
     echo "Removing existing container: ${container_name}"
     docker rm -f "${container_name}"
   fi 
}

test_postgres_container() {
  local container_name="fluent-cms-db-postgres"
  local init_sql_path=$1
  
  remove_container $container_name
  local docker_run_command="docker run -d --name $container_name -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=mysecretpassword -e POSTGRES_DB=fluent-cms -p 5432:5432"
  
  # Add volume mapping if init_sql_path is not empty
  if [ -n "$init_sql_path" ]; then
    docker_run_command+=" -v $init_sql_path:/docker-entrypoint-initdb.d/init.sql"
  fi
  docker_run_command+=" postgres:latest"
  eval "$docker_run_command"
  
  export DatabaseProvider=Postgres
  export Postgres="Host=localhost;Database=fluent-cms;Username=postgres;Password=mysecretpassword"
  dotnet test
}

test_sqlserver_container(){
  local container_name="fluent-cms-db-sql-edge"
  local password=Admin12345678!
  remove_container $container_name

 # docker run --cap-add SYS_PTRACE -e 'ACCEPT_EULA=1' -e "MSSQL_SA_PASSWORD=$password" -p 1433:1433 --name $container_name -d mcr.microsoft.com/azure-sql-edge
  docker run --cap-add SYS_PTRACE -e 'ACCEPT_EULA=1' -e "MSSQL_SA_PASSWORD=$password" -p 1433:1433 --name $container_name -d mcr.microsoft.com/mssql/server:2022-latest 
  sleep 10
  
  export DatabaseProvider=SqlServer
  export SqlServer="Server=localhost;Database=cms;User Id=sa;Password=Admin12345678!;TrustServerCertificate=True"
  dotnet test  
}

# Exit immediately if a command exits with a non-zero status
set -e
export Logging__LogLevel__Default=Warning
export Logging__LogLevel__Microsoft_AspNetCore=Warning

# Sqlite With Default Data 
db_path=$(pwd)/default.db && rm -f $db_path && cp ../FluentCMS.Blog/cms.db "$db_path" && test_sqlite "$db_path"

# Sqlite With Empty Data 
db_path=$(pwd)/temp.db && rm -f "$db_path" && test_sqlite "$db_path"

test_postgres_container ""

test_sqlserver_container

