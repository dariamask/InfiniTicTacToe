import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { WebSocketService } from '../web-socket.service';
import { ClientHelloMessage, ReadyMessage } from '../models';

@Component({
  selector: 'app-game-controls',
  templateUrl: './game-controls.component.html',
  styleUrls: ['./game-controls.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule]
})
export class GameControlsComponent implements OnInit {
  @Output() startGame: EventEmitter<void> = new EventEmitter();
  @Output() endGame: EventEmitter<void> = new EventEmitter();
  @Output() clientHello: EventEmitter<string> = new EventEmitter();

  nickname: string = '';

  constructor(private webSocketService: WebSocketService) { }

  async ngOnInit() {
    this.nickname = `User${Math.floor(Math.random() * 100000000)}`;
    await this.webSocketService.connectionEstablished;
    this.webSocketService.sendMessage(new ClientHelloMessage(this.nickname))
  }




  onStartGame() {
    console.log('Start Game button clicked');
    const readyMessage = new ReadyMessage();
    this.webSocketService.sendMessage(readyMessage);
  }

  onEndGame() {
    this.endGame.emit();
  }
}
