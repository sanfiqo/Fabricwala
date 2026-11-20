import re,sys
css=open(sys.argv[1] if len(sys.argv)>1 else 'fabricwala-bw.css').read()
v={}
for k,x in re.findall(r'--([\w-]+):\s*(#[0-9A-Fa-f]{6})',css): v.setdefault(k,x)  # first definition = default theme
def L(h):
    h=h.lstrip('#');c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    c=[x/12.92 if x<=.03928 else ((x+.055)/1.055)**2.4 for x in c]
    return .2126*c[0]+.7152*c[1]+.0722*c[2]
def cr(a,b):
    x,y=sorted([L(a),L(b)],reverse=True);return (x+.05)/(y+.05)
checks=[]
for fg in ['tx','tx2']:
    for bg in ['bg','s1','s2']: checks.append((fg,bg,4.5,'text'))
checks+=[('on-ink','ink',4.5,'btn/black bar'),('ln-ctl','bg',3.0,'control border'),('ln-ctl','s1',3.0,'control border')]
for s in ['ok','warn','err','info']: checks.append((s,'bg',4.5,'status text'))
bad=0
for fg,bg,need,k in checks:
    if fg not in v or bg not in v: print('missing',fg,bg);bad+=1;continue
    r=cr(v[fg],v[bg]);ok=r>=need;bad+=not ok;print('PASS' if ok else 'FAIL',f'{r:5.2f}',f'need {need}',fg,v[fg],'on',bg,v[bg],f'[{k}]')
print('decorative divider --ln on white:',round(cr(v['ln'],v['bg']),2),'(decorative only, never a control border)')
print('FAILURES',bad)
