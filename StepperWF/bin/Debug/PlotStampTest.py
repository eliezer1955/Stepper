﻿import numpy as np
import matplotlib.pyplot as plt


def myfloat(s):
    result=None
    for i in s.split():
        try:
            result=float(i)
            break
        except:
            continue
    return result


#load diag log into memory
file_path='diags.log'
with open(file_path, 'r') as file:
    file_contents = file.readlines()
#scan file for last stamp test occurrence
lineno = -1
startline= -1
for line in file_contents:
    lineno += 1
    if line.find("Starting stamp test") > 0:   
        startline=lineno
if startline <0:
    print("stamp test not found")
    exit()
   
#isolate date/time

eofdate=file_contents[startline].find("[")
dateTime=file_contents[startline][0:eofdate]

#build lists of position, force
tgt1="Position= "
tgt2="Force = "
tgt3="SN "
sampleno=0
x=[]
y=[]
for i in range(startline+1,len(file_contents)):
    index=file_contents[i].find(tgt1)
    if index<0:
        continue;
    position= myfloat(file_contents[i][index+len(tgt1):])
    index1=file_contents[i].find(tgt2)
    if index1<0:
        continue;
    index3=file_contents[i].find(tgt3)
    for k in range(index3+len(tgt3)):
        if file_contents[i][k] == " ":
            break;
    sn=file_contents[i][index3+len(tgt3):index3+len(tgt3)+k]
    force=myfloat(file_contents[i][index1+len(tgt2):])
    x.append(position)
    y.append(force)
#convert lists into numpy arrays
x=np.array(x)
y=np.array(y)
#generate plot
fig=plt.figure()
ax=fig.add_subplot(111)
expectedx=[20000,135000,140000]
expectedy=[12,30,100]
ax.plot(expectedx,expectedy,linewidth=12,color="green",alpha=0.4)
ax.plot(x,y)
ax.set_title(dateTime+"_"+sn)
ax.set(xlabel='Position [steps]',ylabel='Force [au]')
plt.subplots_adjust(left=0.15)
plt.subplots_adjust(bottom=0.25)

fname=dateTime+'.png'
fname=fname.replace(" ","_")
fname=fname.replace(":","")
fname=fname.replace(",","_")
#save plot to file
plt.savefig(fname+"_SN_"+sn, format="png", bbox_inches='tight')

plt.show()
    
