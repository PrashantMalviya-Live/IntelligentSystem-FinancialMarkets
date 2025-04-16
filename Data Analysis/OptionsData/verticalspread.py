
import numpy as np
import pandas as pd
from pylab import mpl, plt
plt.style.use('seaborn')
mpl.rcParams['font.family'] = 'serif'
"""%matplotlib inline """
"""%matplotlib qt """
import pyodbc

#from plotly.offline import download_plotlyjs, init_notebook_mode,  plot
#from plotly.graph_objs import *
#init_notebook_mode()


import cufflinks as cf
#import plotly.offline as plyo
#plyo.init_notebook_mode(connected=True)

sql_conn = pyodbc.connect('DRIVER={ODBC Driver 13 for SQL Server}; SERVER=.; DATABASE=NSEData; Trusted_Connection=yes')
query = "Select [Id],[InstrumentToken], LastPrice,	Volume, [LastTradeTime], [OI], [TimeStamp] from [Ticks] \
with (nolock) Where  InstrumentToken in (260105,10443010, 10443266, 11154178, 11154434, 13916162, 13916418) and  lASTPRICE != 0  \
    AND ([LastTradeTime] > '2022-07-07 09:12' AND [LastTradeTime] < '2022-07-07 15:30') \
        AND ( CAST([LastTradeTime] as time) > '09:12' AND CAST([LastTradeTime] as time) < '15:31') \
ORDER by [TimeStamp]  asc"

rawdata = pd.read_sql(query, sql_conn)

itoken = [10443010, 10443266, 11154178, 11154434, 13916162, 13916418]

df = rawdata[rawdata.InstrumentToken.isin(itoken)]
df.head()
df10516738 = df[df['InstrumentToken'] == 10516738]

fig, ax1 = plt.subplots()
plt.plot(df[df['InstrumentToken'] == 10443010]['LastPrice'], 'b')
ax2 = ax1.twinx()
plt.plot(df[df['InstrumentToken'] == 11154178]['LastPrice'], 'g')

#fig = go.Figure(df=df10516738['LastPrice'], layout=layout)

df10516738.index = df10516738['LastTradeTime']
plyo.plot(df10516738.plot())
fig.show()
