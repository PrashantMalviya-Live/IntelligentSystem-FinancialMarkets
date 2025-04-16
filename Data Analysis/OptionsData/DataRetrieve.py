
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
with (nolock) Where  InstrumentToken in (13755906) and  lASTPRICE != 0  \
    AND ([LastTradeTime] > '2022-07-19 09:12' AND [LastTradeTime] < '2021-07-19 15:30') \
        AND ( CAST([LastTradeTime] as time) > '09:12' AND CAST([LastTradeTime] as time) < '15:31') \
ORDER by [TimeStamp]  asc"

rawdata = pd.read_sql(query, sql_conn)

itoken = [13755906,260105]

df = rawdata[rawdata.InstrumentToken.isin(itoken)]
df.head()
df10516738 = df[df['InstrumentToken'] == 10516738]

fig, ax1 = plt.subplots()
plt.plot(df[df['InstrumentToken'] == 10516738]['LastPrice'], 'b')
ax2 = ax1.twinx()
plt.plot(df[df['InstrumentToken'] == 260105]['LastPrice'], 'g')

fig = go.Figure(df=df10516738['LastPrice'], layout=layout)

df10516738.index = df10516738['LastTradeTime']
plyo.plot(df10516738.plot())
fig.show()

import plotly.graph_objs as go
import plotly.offline as pyo
dt = pd.date_range(start='1969-12-31', periods=270, freq='Q')
t1 = []
t1.append(dict(x=dt, y=np.ones(270), name='cape', type='scatter'))
layout = go.Layout(title="test", width=1800, height=930, font=dict(size=27), titlefont=dict(size=42))
fig = go.Figure(data=t1, layout=layout)
pyo.plot(fig, filename='test.html', auto_open=False, show_link=False)
