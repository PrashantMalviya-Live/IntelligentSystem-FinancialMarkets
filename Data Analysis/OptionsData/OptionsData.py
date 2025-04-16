#importing module  
import pypyodbc  
#creating connection Object which will contain SQL Server Connection  
connection = pypyodbc.connect('Driver={SQL Server};Server=.;Database=NSEData;uid=zm;pwd=zm')  
  
print("Connection Successfully Established")  

#Creating Cursor  
cursor = connection.cursor()   
#SQL Query  
SQLCommand = ("Select * from Instruments")  
#Processing Query  
cursor.execute(SQLCommand)   
results = cursor.fetchall()   
i = 0;
while results:
    print(results[i])
    i = i+1

#while results:  
#    print ("Name:" +  str(results[0]))  
#    print ("Salary:" +  str(results[1]))  
#    print ("Mobile:" +  str(results[2]))  
#    print ("Designation:" +  str(results[3]))  
#    print()  
#    results = cursor.fetchone() 
#Commiting any pending transaction to the database.  
connection.commit()  

#closing connection  
connection.close()  





