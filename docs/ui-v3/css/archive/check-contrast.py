import re,sys
css=open(sys.argv[1]).read()
v={}
for m in re.finditer(r'--([\w-]+):\s*(#[0-9A-Fa-f]{6})',css): v.setdefault(m.group(1),m.group(2))
def g(n):
    x=v[n]
    return x
def L(h):
    h=h.lstrip('#');c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    c=[x/12.92 if x<=.03928 else ((x+.055)/1.055)**2.4 for x in c]
    return .2126*c[0]+.7152*c[1]+.0722*c[2]
def cr(a,b):
    x,y=sorted([L(a),L(b)],reverse=True);return (x+.05)/(y+.05)
bgs=['ivory','porcelain','sand']
checks=[]
for fg,need in [('espresso',4.5),('c-text-secondary',4.5),('c-text-muted',4.5),('c-placeholder',4.5),('mocha-deep',4.5)]:
    for bg in (bgs if fg!='c-placeholder' else ['ivory','porcelain']): checks.append((fg,bg,need,'text'))
for fg in ['c-border-control','mocha-deep']:
    for bg in ['ivory','porcelain']: checks.append((fg,bg,3.0,'UI/focus'))
checks += [('c-text-secondary','c-surface-subtle',4.5,'text')] if 'c-surface-subtle' in v else []
for s in ['success','warning','danger','info']:
    checks.append((f'c-{s}',f'c-{s}-bg',4.5,'badge'))
    checks.append((f'c-{s}','porcelain',4.5,'text'))
checks += [('c-on-action','c-action',4.5,'btn'),('c-on-action','c-action-hover',4.5,'btn hover'),('c-on-action','c-action-active',4.5,'btn active'),
           ('mocha','porcelain',4.5,'(info) raw mocha as text'),('mocha','ivory',4.5,'(info) raw mocha as text'),
           ('taupe','porcelain',3.0,'(info) raw taupe as border'),('c-border','porcelain',3.0,'(info) decorative border')]
v['c-action']=v['espresso']
bad=0
for fg,bg,need,kind in checks:
    f=v.get(fg);b=v.get(bg)
    if not f or not b: print('skip',fg,bg);continue
    r=cr(f,b);ok=r>=need
    info=kind.startswith('(info)')
    print(f"{'PASS' if ok else ('n/a ' if info else 'FAIL')} {r:5.2f} need {need}  {fg} {f} on {bg} {b}  [{kind}]")
    if not ok and not info: bad+=1
# white text on danger button
for h in ['#B42318','#8F1B12']: print('btn--danger white on',h,round(cr('#FFFFFF',h),2))
print('FAILURES:',bad)
