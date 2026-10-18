const path = require('node:path');
const root=path.resolve(process.argv[2]);
const {loadGameData}=require(root+'/dist/infrastructure/data/loadGameData');
const {GameSession}=require(root+'/dist/models/GameSession');
const {SessionService}=require(root+'/dist/services/SessionService');
const {ParticipantRegistry}=require(root+'/dist/services/ParticipantRegistry');
const {SessionController}=require(root+'/dist/controllers/SessionController');
const {createSessionHttpServer}=require(root+'/dist/infrastructure/http/createSessionHttpServer');
loadGameData(root+'/data').then(data=>{
 const sessions=new SessionService(new GameSession({id:'unity-world-verification',roles:['A','B']}),new ParticipantRegistry({nowMs:()=>Date.now()},60000),data);
 const server=createSessionHttpServer(new SessionController(sessions));
 server.listen(3099,'127.0.0.1',()=>process.stdout.write('ready\n'));
});
