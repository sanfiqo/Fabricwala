def L(h):
    h=h.lstrip('#');c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    c=[x/12.92 if x<=.03928 else ((x+.055)/1.055)**2.4 for x in c]
    return .2126*c[0]+.7152*c[1]+.0722*c[2]
def cr(a,b):
    x,y=sorted([L(a),L(b)],reverse=True);return (x+.05)/(y+.05)
bgs={'canvas':'#EAF6F3','white':'#FFFFFF','aqua':'#D4F1EE','mint':'#E6F4EF','peach':'#FFE4D4','subtle':'#D6EFEC'}
rows=[]
for fg,n,need,on in [('#0F2427','text',4.5,bgs),('#4A5F62','secondary',4.5,bgs),('#4F6568','muted',4.5,bgs),('#0A7479','accent-text',4.5,bgs),('#5B7073','placeholder',4.5,{'white':'#FFFFFF','canvas':'#EAF6F3'}),('#6F8D8F','control border',3.0,{'white':'#FFFFFF','canvas':'#EAF6F3'})]:
    for k,b in on.items(): rows.append((n,k,cr(fg,b),need))
for n,b in [('btn1','#0A7C84'),('btn2','#087A66'),('hover','#08666D'),('active','#064F55')]: rows.append(('white on '+n,'',cr('#FFFFFF',b),4.5))
bad=0
for n,k,r,need in rows:
    ok=r>=need;bad+=not ok;print(('PASS' if ok else 'FAIL'),f'{r:5.2f}',f'need {need}',n,k)
print('FAILURES',bad)
