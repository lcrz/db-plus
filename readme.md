Para ejecutar la aplicacion:

dotnet run --project DbClient.Wpf

Para publicar la aplicacion:

dotnet publish DbClient.Wpf -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

Ubicacion de la publicacion:

C:\proyectos\db+\DbClient.Wpf\bin\Release\net10.0-windows\win-x64\publish