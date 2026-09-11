"""Deterministic HTTP contract tests. No external provider requests are made."""
import http.cookiejar,json,os,socket,subprocess,tempfile,time,urllib.request,urllib.error
from pathlib import Path
root=Path(__file__).resolve().parents[1]
with socket.socket() as sock:
 sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
base=f'http://127.0.0.1:{port}'
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(path,body=None,code=200,client=opener):
 req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
 try:
  with client.open(req,timeout=30) as res: status=res.status; data=res.read()
 except urllib.error.HTTPError as ex: status=ex.code;data=ex.read()
 assert status==code,(path,status,data.decode()[:500])
 return json.loads(data) if data else None
with tempfile.TemporaryDirectory() as directory:
 env={**os.environ,'ASPNETCORE_URLS':base,'ASPNETCORE_ENVIRONMENT':'Testing','TestFixtures':'true','DATA_DIR':directory}
 with open(Path(directory)/'server.log','w+') as log:
  process=subprocess.Popen([os.environ.get('DOTNET','dotnet'),str(root/'src/bin/Release/net10.0/App.dll'),'--contentRoot',str(root/'src')],env=env,stdout=log,stderr=log,cwd=root)
  try:
   for _ in range(100):
    try:
     assert request('/api/health')['mode']=='test fixtures';break
    except (OSError,AssertionError): time.sleep(.1)
   else: raise RuntimeError('Server did not start')
   body={'csv':'id,currency,amount\na,USD,120\nb,EUR,1.005','reportingCurrency':'EUR','date':'2026-09-01','shockPercent':10,'requestId':'repeatable'}
   body['csv']=body['csv'].replace('\\n','\n')
   a=request('/api/calculate',body);s=a['snapshot'];assert s['baselineTotal']==101 and s['scenarioTotal']==111
   b=request('/api/calculate',body);assert b['id']==a['id'] and b['reused']
   request('/api/calculate',{**body,'shockPercent':20},409)
   assert request('/api/snapshots/'+a['id'])==s
   request('/api/calculate',{**body,'requestId':'bad','reportingCurrency':'XXX'},400)
   request('/api/calculate',{**body,'requestId':'bad','date':'2099-01-01'},400)
   request('/api/calculate',{**body,'requestId':'bad','shockPercent':51},400)
   request('/api/calculate',{**body,'requestId':'bad','csv':'id,currency,amount\na,USD,1\na,USD,2'.replace('\\n','\n')},400)
   print('HTTP decimal totals, scenario, stored evidence, idempotency conflict and input bounds passed.')
  finally:
   process.terminate()
   try:process.wait(timeout=10)
   except subprocess.TimeoutExpired:process.kill();process.wait()
